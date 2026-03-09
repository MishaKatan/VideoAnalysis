using VideoAnalysis.Core.Models;
using VideoAnalysis.Core.Services;

namespace VideoAnalysis.Tests;

public sealed class TagServiceTests
{
    private readonly TagService _tagService = new();

    [Fact]
    public void Validate_Throws_When_EndFrameLessThanStart()
    {
        var tagEvent = new TagEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 120, 10, null, null, null, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => _tagService.Validate(tagEvent));
    }

    [Fact]
    public void MergeOverlapping_MergesAdjacentEvents()
    {
        var projectId = Guid.NewGuid();
        var presetId = Guid.NewGuid();
        var events = new[]
        {
            new TagEvent(Guid.NewGuid(), projectId, presetId, 10, 20, "A", "1", "first", DateTimeOffset.UtcNow),
            new TagEvent(Guid.NewGuid(), projectId, presetId, 21, 35, "A", "1", "second", DateTimeOffset.UtcNow)
        };

        var merged = _tagService.MergeOverlapping(events);
        Assert.Single(merged);
        Assert.Equal(10, merged[0].StartFrame);
        Assert.Equal(35, merged[0].EndFrame);
        Assert.Contains("first", merged[0].Notes);
        Assert.Contains("second", merged[0].Notes);
    }

    [Fact]
    public void Filter_FiltersByPlayerAndText()
    {
        var projectId = Guid.NewGuid();
        var goalPreset = new TagPreset(Guid.NewGuid(), projectId, "Goal", "#FF0000", "GameEvent", true);
        var savePreset = new TagPreset(Guid.NewGuid(), projectId, "Save", "#00FF00", "Goalie", true);
        var events = new[]
        {
            new TagEvent(Guid.NewGuid(), projectId, goalPreset.Id, 30, 60, "Player A", "2", "slot chance", DateTimeOffset.UtcNow),
            new TagEvent(Guid.NewGuid(), projectId, savePreset.Id, 70, 90, "Goalie B", "2", "high danger", DateTimeOffset.UtcNow)
        };

        var result = _tagService.Filter(
            events,
            new TagQuery(null, "Player A", null, "chance"),
            new Dictionary<Guid, TagPreset> { [goalPreset.Id] = goalPreset, [savePreset.Id] = savePreset });

        Assert.Single(result);
        Assert.Equal("Player A", result[0].Player);
    }
}
