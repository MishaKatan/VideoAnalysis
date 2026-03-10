using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.App.ViewModels.Items;

public sealed class AnnotationItemViewModel
{
    public required Guid Id { get; init; }
    public required AnnotationShapeType ShapeType { get; init; }
    public required long StartFrame { get; init; }
    public required long EndFrame { get; init; }
    public required double X1 { get; init; }
    public required double Y1 { get; init; }
    public required double X2 { get; init; }
    public required double Y2 { get; init; }
    public required string ColorHex { get; init; }
    public required string Text { get; init; }
}
