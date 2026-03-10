using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Core.Abstractions;

public interface IEventCaptureService
{
    Task<TagEvent> RegisterHotkeyPressAsync(
        Guid projectId,
        string hotkey,
        long frame,
        TeamSide teamSide,
        CancellationToken cancellationToken);
}
