namespace VideoAnalysis.Core.Models;

public sealed record MediaAsset(
    Guid Id,
    Guid ProjectId,
    string FilePath,
    double FramesPerSecond,
    long DurationFrames,
    long Width,
    long Height,
    DateTimeOffset ImportedAtUtc);
