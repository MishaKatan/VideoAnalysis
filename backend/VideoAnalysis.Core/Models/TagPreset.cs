namespace VideoAnalysis.Core.Models;

public sealed record TagPreset(
    Guid Id,
    Guid ProjectId,
    string Name,
    string ColorHex,
    string Category,
    bool IsSystem,
    string Hotkey = "",
    string IconKey = "event",
    bool ShowInStatistics = true);


