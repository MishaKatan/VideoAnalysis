using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Core.Abstractions;

public interface ITagService
{
    void Validate(TagEvent tagEvent);
    bool Overlaps(TagEvent left, TagEvent right);
    IReadOnlyList<TagEvent> MergeOverlapping(IEnumerable<TagEvent> events);
    IReadOnlyList<TagEvent> Filter(IEnumerable<TagEvent> events, TagQuery query, IReadOnlyDictionary<Guid, TagPreset> presetsById);
    TagEventDto ToDto(TagEvent tagEvent, TagPreset preset);
}
