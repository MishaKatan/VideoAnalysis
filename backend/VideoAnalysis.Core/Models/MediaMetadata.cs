namespace VideoAnalysis.Core.Models;

public sealed record MediaMetadata(
    string FilePath,
    double FramesPerSecond,
    long DurationFrames,
    long Width,
    long Height);
