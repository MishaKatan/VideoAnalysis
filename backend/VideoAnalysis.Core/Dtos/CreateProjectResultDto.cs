namespace VideoAnalysis.Core.Dtos;

public sealed record CreateProjectResultDto(
    Guid ProjectId,
    string ProjectFolderPath,
    string StoredVideoPath,
    string VideoTitle);
