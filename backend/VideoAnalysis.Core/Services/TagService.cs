using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Core.Services;

public sealed class TagService : ITagService
{
    public void Validate(TagEvent tagEvent)
    {
        if (tagEvent.StartFrame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tagEvent.StartFrame), "Start frame must be >= 0.");
        }

        if (tagEvent.EndFrame < tagEvent.StartFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(tagEvent.EndFrame), "End frame must be >= start frame.");
        }

        if (!Enum.IsDefined(tagEvent.TeamSide))
        {
            throw new ArgumentOutOfRangeException(nameof(tagEvent.TeamSide), "Team side value is invalid.");
        }
    }

    public bool Overlaps(TagEvent left, TagEvent right)
    {
        Validate(left);
        Validate(right);
        return left.StartFrame <= right.EndFrame && right.StartFrame <= left.EndFrame;
    }

    public IReadOnlyList<TagEvent> MergeOverlapping(IEnumerable<TagEvent> events)
    {
        var ordered = events.OrderBy(x => x.StartFrame).ThenBy(x => x.EndFrame).ToList();
        if (ordered.Count == 0)
        {
            return ordered;
        }

        var merged = new List<TagEvent>();
        var current = ordered[0];

        for (var index = 1; index < ordered.Count; index++)
        {
            var next = ordered[index];
            if (Overlaps(current, next) || current.EndFrame + 1 == next.StartFrame)
            {
                current = current with
                {
                    EndFrame = Math.Max(current.EndFrame, next.EndFrame),
                    Notes = string.Join("; ", new[] { current.Notes, next.Notes }.Where(x => !string.IsNullOrWhiteSpace(x)))
                };
                continue;
            }

            merged.Add(current);
            current = next;
        }

        merged.Add(current);
        return merged;
    }

    public IReadOnlyList<TagEvent> Filter(IEnumerable<TagEvent> events, TagQuery query, IReadOnlyDictionary<Guid, TagPreset> presetsById)
    {
        var result = events.Where(x =>
            (!query.TagPresetId.HasValue || x.TagPresetId == query.TagPresetId.Value) &&
            (string.IsNullOrWhiteSpace(query.Player) || string.Equals(x.Player, query.Player, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(query.Period) || string.Equals(x.Period, query.Period, StringComparison.OrdinalIgnoreCase)) &&
            (!query.TeamSide.HasValue || x.TeamSide == query.TeamSide.Value) &&
            (!query.IsOpen.HasValue || x.IsOpen == query.IsOpen.Value) &&
            (
                string.IsNullOrWhiteSpace(query.Text) ||
                (!string.IsNullOrWhiteSpace(x.Notes) && x.Notes.Contains(query.Text, StringComparison.OrdinalIgnoreCase)) ||
                (presetsById.TryGetValue(x.TagPresetId, out var preset) && preset.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase))
            ));

        return result.OrderBy(x => x.StartFrame).ThenBy(x => x.EndFrame).ToList();
    }

    public TagEventDto ToDto(TagEvent tagEvent, TagPreset preset)
    {
        return new TagEventDto(
            tagEvent.Id,
            tagEvent.TagPresetId,
            preset.Name,
            tagEvent.StartFrame,
            tagEvent.EndFrame,
            tagEvent.Player,
            tagEvent.Period,
            tagEvent.Notes,
            tagEvent.TeamSide,
            tagEvent.IsOpen,
            preset.Hotkey,
            preset.IconKey,
            preset.ColorHex);
    }
}
