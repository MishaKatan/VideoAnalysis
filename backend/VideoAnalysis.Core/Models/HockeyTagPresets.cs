namespace VideoAnalysis.Core.Models;

public static class HockeyTagPresets
{
    public static IReadOnlyList<TagPreset> CreateDefaults(Guid projectId)
    {
        return
        [
            new TagPreset(Guid.NewGuid(), projectId, "Гол", "#E53935", "Атака", true, "G", "goal", true),
            new TagPreset(Guid.NewGuid(), projectId, "Бросок", "#1E88E5", "Атака", true, "B", "shot", true),
            new TagPreset(Guid.NewGuid(), projectId, "Удаление", "#FB8C00", "Нарушение", true, "U", "penalty", true),
            new TagPreset(Guid.NewGuid(), projectId, "Силовой прием", "#8E24AA", "Борьба", true, "H", "hit", true),
            new TagPreset(Guid.NewGuid(), projectId, "Выход из своей зоны", "#00897B", "Тактика", true, "Z", "zone-exit", true),
            new TagPreset(Guid.NewGuid(), projectId, "Атака", "#43A047", "Тактика", true, "A", "attack", true),
            new TagPreset(Guid.NewGuid(), projectId, "Защита", "#3949AB", "Тактика", true, "D", "defense", true),
            new TagPreset(Guid.NewGuid(), projectId, "Заблокированный бросок", "#6D4C41", "Защита", true, "K", "blocked-shot", true),
            new TagPreset(Guid.NewGuid(), projectId, "Отбор", "#546E7A", "Защита", true, "O", "steal", true),
            new TagPreset(Guid.NewGuid(), projectId, "Потеря", "#C62828", "Ошибка", true, "T", "turnover", true),
            new TagPreset(Guid.NewGuid(), projectId, "Просмотр", "#8D6E63", "Судьи", true, "M", "review", true),
            new TagPreset(Guid.NewGuid(), projectId, "Опасный момент", "#D81B60", "Атака", true, "N", "chance", true),
            new TagPreset(Guid.NewGuid(), projectId, "Смена", "#6A1B9A", "Тактика", true, "C", "line-change", true),
            new TagPreset(Guid.NewGuid(), projectId, "Вбрасывание", "#5E35B1", "Тактика", true, "F", "faceoff", true)
        ];
    }
}
