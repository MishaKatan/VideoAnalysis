using VideoAnalysis.Core.Models;

namespace VideoAnalysis.App.ViewModels.Items;

public sealed class EventTypeItemViewModel
{
    public required TagPreset Preset { get; init; }
    public required int EventCount { get; init; }

    public Guid Id => Preset.Id;
    public string Name => Preset.Name;
    public string ColorHex => Preset.ColorHex;
    public string Category => Preset.Category;
    public string Hotkey => Preset.Hotkey;
}
