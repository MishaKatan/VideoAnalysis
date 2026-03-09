using System.Diagnostics;
using System.Globalization;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Services;

public sealed class FfmpegClipComposerService : IClipComposerService
{
    private readonly string _ffmpegPath;

    public FfmpegClipComposerService(string ffmpegPath)
    {
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    public IReadOnlyList<ClipSegmentDto> BuildSegments(IEnumerable<TagEvent> events, ClipRecipe recipe, long maxFrame)
    {
        return events
            .Where((tagEvent) =>
                (!recipe.TagPresetId.HasValue || tagEvent.TagPresetId == recipe.TagPresetId.Value) &&
                (string.IsNullOrWhiteSpace(recipe.Player) || string.Equals(tagEvent.Player, recipe.Player, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(recipe.Period) || string.Equals(tagEvent.Period, recipe.Period, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(recipe.QueryText) || (!string.IsNullOrWhiteSpace(tagEvent.Notes) && tagEvent.Notes.Contains(recipe.QueryText, StringComparison.OrdinalIgnoreCase))))
            .Select((tagEvent) =>
            {
                var start = Math.Max(0, tagEvent.StartFrame - recipe.PreRollFrames);
                var end = Math.Min(maxFrame, tagEvent.EndFrame + recipe.PostRollFrames);
                return new ClipSegmentDto(tagEvent.Id, start, end, recipe.Name, tagEvent.Player);
            })
            .OrderBy((segment) => segment.StartFrame)
            .ToList();
    }

    public async Task<string> ComposeAsync(
        string sourceVideoPath,
        IReadOnlyList<ClipSegmentDto> segments,
        string outputPath,
        double framesPerSecond,
        string? overlayFilterPath,
        CancellationToken cancellationToken)
    {
        if (segments.Count == 0)
        {
            throw new InvalidOperationException("No segments were provided.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory);
        var tempRoot = Path.Combine(Path.GetTempPath(), "video-analysis", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var segmentFiles = new List<string>(segments.Count);
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var startSeconds = segment.StartFrame / framesPerSecond;
                var durationSeconds = Math.Max(0.02, (segment.EndFrame - segment.StartFrame + 1) / framesPerSecond);
                var partPath = Path.Combine(tempRoot, $"part_{index:D4}.mp4");
                segmentFiles.Add(partPath);

                var args = string.Join(' ',
                    "-y",
                    $"-ss {ToInvariant(startSeconds)}",
                    $"-i {Quote(sourceVideoPath)}",
                    $"-t {ToInvariant(durationSeconds)}",
                    "-c:v libx264 -preset veryfast -crf 20",
                    "-c:a aac -b:a 160k",
                    Quote(partPath));

                await RunFfmpegAsync(args, cancellationToken);
            }

            var listPath = Path.Combine(tempRoot, "concat.txt");
            await File.WriteAllTextAsync(listPath, string.Join(Environment.NewLine, segmentFiles.Select((path) => $"file '{path.Replace("'", "''")}'")), cancellationToken);
            var mergedPath = string.IsNullOrWhiteSpace(overlayFilterPath) ? outputPath : Path.Combine(tempRoot, "merged.mp4");

            await RunFfmpegAsync($"-y -f concat -safe 0 -i {Quote(listPath)} -c copy {Quote(mergedPath)}", cancellationToken);

            if (!string.IsNullOrWhiteSpace(overlayFilterPath))
            {
                await RunFfmpegAsync(
                    $"-y -i {Quote(mergedPath)} -filter_script:v {Quote(overlayFilterPath!)} -c:v libx264 -preset veryfast -crf 20 -c:a copy {Quote(outputPath)}",
                    cancellationToken);
            }

            return outputPath;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private async Task RunFfmpegAsync(string arguments, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Unable to start ffmpeg process.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var stdErr = await stdErrTask;
            var stdOut = await stdOutTask;
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}. {stdErr}{stdOut}");
        }
    }

    private static string ToInvariant(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
