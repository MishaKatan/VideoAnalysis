namespace VideoAnalysis.Core.Dtos;

public sealed record ExportRequestDto(
    Guid ProjectId,
    string SourceVideoPath,
    IReadOnlyList<ClipSegmentDto> Segments,
    IReadOnlyList<AnnotationDto> Annotations,
    string OutputPath,
    double FramesPerSecond,
    bool UploadToCloud,
    YandexS3Options? Yandex);
