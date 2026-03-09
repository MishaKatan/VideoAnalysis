namespace VideoAnalysis.Core.Dtos;

public sealed record ExportResultDto(
    bool Success,
    string OutputPath,
    string? RemoteObjectKey,
    string? RemoteUrl,
    string? ErrorMessage);
