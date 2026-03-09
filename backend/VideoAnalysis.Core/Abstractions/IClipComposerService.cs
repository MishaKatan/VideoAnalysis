using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Core.Abstractions;

public interface IClipComposerService
{
    IReadOnlyList<ClipSegmentDto> BuildSegments(IEnumerable<TagEvent> events, ClipRecipe recipe, long maxFrame);
    Task<string> ComposeAsync(
        string sourceVideoPath,
        IReadOnlyList<ClipSegmentDto> segments,
        string outputPath,
        double framesPerSecond,
        string? overlayFilterPath,
        CancellationToken cancellationToken);
}
