using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Models;

public sealed record Annotation(
    Guid Id,
    Guid ProjectId,
    Guid? TagEventId,
    long StartFrame,
    long EndFrame,
    AnnotationShapeType ShapeType,
    double X1,
    double Y1,
    double X2,
    double Y2,
    string? Text,
    string ColorHex,
    double StrokeWidth);
