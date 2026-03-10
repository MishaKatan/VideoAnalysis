namespace VideoAnalysis.Core.Models;

public static class HockeyTagPresets
{
    public static IReadOnlyList<TagPreset> CreateDefaults(Guid projectId)
    {
        return
        [
            new TagPreset(Guid.NewGuid(), projectId, "Goal", "#E53935", "GameEvent", true, "G", "goal"),
            new TagPreset(Guid.NewGuid(), projectId, "Shot On Goal", "#43A047", "Offense", true, "S", "shot"),
            new TagPreset(Guid.NewGuid(), projectId, "Penalty", "#FB8C00", "GameEvent", true, "P", "penalty"),
            new TagPreset(Guid.NewGuid(), projectId, "Save", "#1E88E5", "Goalie", true, "V", "save"),
            new TagPreset(Guid.NewGuid(), projectId, "Faceoff", "#5E35B1", "Tactics", true, "F", "faceoff")
        ];
    }
}
