using System.Globalization;
using System.Text;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Infrastructure.Services;

public sealed class AnnotationRenderService : IAnnotationRenderService
{
    public async Task<string?> BuildOverlayFilterScriptAsync(
        IReadOnlyList<AnnotationDto> annotations,
        double framesPerSecond,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (annotations.Count == 0)
        {
            return null;
        }

        Directory.CreateDirectory(workingDirectory);
        var scriptPath = Path.Combine(workingDirectory, "overlay_filters.txt");
        var filters = new List<string>(annotations.Count);

        foreach (var annotation in annotations)
        {
            var startSeconds = annotation.StartFrame / framesPerSecond;
            var endSeconds = annotation.EndFrame / framesPerSecond;
            var enable = $"between(t,{ToInvariant(startSeconds)},{ToInvariant(endSeconds)})";
            var color = NormalizeColor(annotation.ColorHex);

            switch (annotation.ShapeType)
            {
                case AnnotationShapeType.Arrow:
                    filters.Add($"drawtext=text='->':x={ToInvariant(annotation.X1)}:y={ToInvariant(annotation.Y1)}:fontsize=42:fontcolor={color}:enable='{enable}'");
                    break;

                case AnnotationShapeType.Circle:
                    var left = Math.Min(annotation.X1, annotation.X2);
                    var top = Math.Min(annotation.Y1, annotation.Y2);
                    var width = Math.Abs(annotation.X2 - annotation.X1);
                    var height = Math.Abs(annotation.Y2 - annotation.Y1);
                    filters.Add($"drawbox=x={ToInvariant(left)}:y={ToInvariant(top)}:w={ToInvariant(width)}:h={ToInvariant(height)}:color={color}:t={ToInvariant(annotation.StrokeWidth)}:enable='{enable}'");
                    break;

                case AnnotationShapeType.Text:
                    var text = EscapeText(string.IsNullOrWhiteSpace(annotation.Text) ? "NOTE" : annotation.Text!);
                    filters.Add($"drawtext=text='{text}':x={ToInvariant(annotation.X1)}:y={ToInvariant(annotation.Y1)}:fontsize=30:fontcolor={color}:enable='{enable}'");
                    break;
            }
        }

        if (filters.Count == 0)
        {
            return null;
        }

        await File.WriteAllTextAsync(scriptPath, string.Join(',', filters), Encoding.UTF8, cancellationToken);
        return scriptPath;
    }

    private static string NormalizeColor(string input)
    {
        return string.IsNullOrWhiteSpace(input) ? "white" : input.Trim().TrimStart('#');
    }

    private static string EscapeText(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal);
    }

    private static string ToInvariant(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
