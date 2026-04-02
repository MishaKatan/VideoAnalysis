using VideoAnalysis.Core.Dtos;

namespace VideoAnalysis.Core.Abstractions;

public interface IAnnotationRenderService
{
    Task<string?> BuildOverlayFilterScriptAsync(
        IReadOnlyList<AnnotationDto> annotations,
        IReadOnlyList<ClipSegmentDto> segments,
        double framesPerSecond,
        string workingDirectory,
        CancellationToken cancellationToken);
}
