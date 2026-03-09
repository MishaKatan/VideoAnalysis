using VideoAnalysis.Core.Enums;

namespace VideoAnalysis.Core.Models;

public sealed record ExportJob(
    Guid Id,
    Guid ProjectId,
    Guid? ClipRecipeId,
    ExportDestinationType Destination,
    string OutputPath,
    string? RemoteObjectKey,
    ExportJobStatus Status,
    string? Error,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
