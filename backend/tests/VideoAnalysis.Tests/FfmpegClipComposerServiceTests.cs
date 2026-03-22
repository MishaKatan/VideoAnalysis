using VideoAnalysis.Core.Models;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Infrastructure.Services;

namespace VideoAnalysis.Tests;

public sealed class FfmpegClipComposerServiceTests
{
    [Fact]
    public void BuildSegments_AppliesRecipeAndRollFrames()
    {
        var service = new FfmpegClipComposerService("ffmpeg");
        var projectId = Guid.NewGuid();
        var targetPresetId = Guid.NewGuid();
        var events = new[]
        {
            new TagEvent(Guid.NewGuid(), projectId, targetPresetId, 100, 150, "Player A", "1", null, DateTimeOffset.UtcNow),
            new TagEvent(Guid.NewGuid(), projectId, Guid.NewGuid(), 300, 340, "Player B", "2", null, DateTimeOffset.UtcNow)
        };

        var recipe = new ClipRecipe(Guid.NewGuid(), projectId, "Goals", targetPresetId, null, null, null, 10, 20, DateTimeOffset.UtcNow);
        var segments = service.BuildSegments(events, recipe, 1000);

        Assert.Single(segments);
        Assert.Equal(90, segments[0].StartFrame);
        Assert.Equal(170, segments[0].EndFrame);
    }

    [Fact]
    public async Task ComposeAsync_WhenFfmpegIsMissing_ThrowsHelpfulError()
    {
        var missingExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "ffmpeg.exe");
        var service = new FfmpegClipComposerService(missingExecutablePath);
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        IReadOnlyList<ClipSegmentDto> segments =
        [
            new ClipSegmentDto(Guid.NewGuid(), 0, 15, "Clip", null)
        ];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ComposeAsync("source.mp4", segments, outputPath, 25, null, CancellationToken.None));

        Assert.Contains("FFmpeg", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("settings.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
