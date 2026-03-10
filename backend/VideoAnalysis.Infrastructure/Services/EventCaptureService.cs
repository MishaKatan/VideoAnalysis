using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Services;

public sealed class EventCaptureService : IEventCaptureService
{
    private readonly IProjectRepository _repository;
    private readonly ITagService _tagService;

    public EventCaptureService(IProjectRepository repository, ITagService tagService)
    {
        _repository = repository;
        _tagService = tagService;
    }

    public async Task<TagEvent> RegisterHotkeyPressAsync(
        Guid projectId,
        string hotkey,
        long frame,
        TeamSide teamSide,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id must be set.", nameof(projectId));
        }

        if (frame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "Frame must be >= 0.");
        }

        var normalizedHotkey = NormalizeHotkey(hotkey);
        if (string.IsNullOrWhiteSpace(normalizedHotkey))
        {
            throw new ArgumentException("Hotkey is required.", nameof(hotkey));
        }

        var presets = await _repository.GetTagPresetsAsync(projectId, cancellationToken);
        var preset = presets.FirstOrDefault((x) => string.Equals(NormalizeHotkey(x.Hotkey), normalizedHotkey, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            throw new InvalidOperationException($"No event type is mapped to hotkey '{normalizedHotkey}'.");
        }

        var openEvents = await _repository.GetTagEventsAsync(
            projectId,
            new TagQuery(preset.Id, null, null, null, null, true),
            cancellationToken);

        var openEvent = openEvents.OrderByDescending((x) => x.StartFrame).FirstOrDefault();
        TagEvent storedEvent;

        if (openEvent is null)
        {
            storedEvent = new TagEvent(
                Guid.NewGuid(),
                projectId,
                preset.Id,
                frame,
                frame,
                null,
                null,
                null,
                DateTimeOffset.UtcNow,
                teamSide,
                true);
        }
        else
        {
            storedEvent = openEvent with
            {
                EndFrame = Math.Max(openEvent.StartFrame, frame),
                IsOpen = false,
                TeamSide = openEvent.TeamSide == TeamSide.Unknown ? teamSide : openEvent.TeamSide
            };
        }

        _tagService.Validate(storedEvent);
        await _repository.UpsertTagEventAsync(storedEvent, cancellationToken);
        return storedEvent;
    }

    private static string NormalizeHotkey(string hotkey) => hotkey.Trim();
}
