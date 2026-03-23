using System.Collections.ObjectModel;

namespace VideoAnalysis.App.ViewModels.Items;

public sealed class TimelineRowItemViewModel
{
    public required Guid PresetId { get; init; }
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
    public ObservableCollection<TimelineEventSegmentItemViewModel> Segments { get; } = [];
}
