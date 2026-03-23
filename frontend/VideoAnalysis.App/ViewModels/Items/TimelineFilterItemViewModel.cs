using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAnalysis.App.ViewModels.Items;

public sealed partial class TimelineFilterItemViewModel : ObservableObject
{
    public required Guid PresetId { get; init; }
    public required string Name { get; init; }
    public required string ColorHex { get; init; }

    [ObservableProperty]
    private bool _isVisible = true;
}
