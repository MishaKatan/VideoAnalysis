using VideoAnalysis.Core.Models;
using VideoAnalysis.Infrastructure.Services;

namespace VideoAnalysis.Tests;

public sealed class FfmpegClipComposerServiceTests
{
    [Fact]
    public void BuildSegments_AppliesRecipeAndRollFrames()
    {
        var service = new FfmpegClipComposerService("ffmpeg");
        var projectId = Guid.NewGuid();
        var targetPresetId = Guid.NewGuid();
        var events = new[]
        {
            new TagEvent(Guid.NewGuid(), projectId, targetPresetId, 100, 150, "Player A", "1", null, DateTimeOffset.UtcNow),
            new TagEvent(Guid.NewGuid(), projectId, Guid.NewGuid(), 300, 340, "Player B", "2", null, DateTimeOffset.UtcNow)
        };

        var recipe = new ClipRecipe(Guid.NewGuid(), projectId, "Goals", targetPresetId, null, null, null, 10, 20, DateTimeOffset.UtcNow);
        var segments = service.BuildSegments(events, recipe, 1000);

        Assert.Single(segments);
        Assert.Equal(90, segments[0].StartFrame);
        Assert.Equal(170, segments[0].EndFrame);
    }
}
