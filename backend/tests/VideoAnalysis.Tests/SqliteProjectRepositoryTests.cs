using VideoAnalysis.Core.Models;
using VideoAnalysis.Infrastructure.Persistence;

namespace VideoAnalysis.Tests;

public sealed class SqliteProjectRepositoryTests : IDisposable
{
    private readonly string _dbPath;

    public SqliteProjectRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "video-analysis-tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
    }

    [Fact]
    public async Task CreateAndLoadProject_Works()
    {
        var repository = new SqliteProjectRepository(_dbPath);
        await repository.InitializeAsync(CancellationToken.None);

        var project = new Project(Guid.NewGuid(), "Test Match", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await repository.CreateProjectAsync(project, CancellationToken.None);

        var loaded = await repository.GetProjectAsync(project.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(project.Name, loaded!.Name);
    }

    [Fact]
    public async Task UpsertAndQueryTags_Works()
    {
        var repository = new SqliteProjectRepository(_dbPath);
        await repository.InitializeAsync(CancellationToken.None);

        var projectId = Guid.NewGuid();
        await repository.CreateProjectAsync(new Project(projectId, "Tag Test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), CancellationToken.None);
        var preset = new TagPreset(Guid.NewGuid(), projectId, "Goal", "#FF0000", "GameEvent", true);
        await repository.UpsertTagPresetAsync(preset, CancellationToken.None);

        var tagEvent = new TagEvent(Guid.NewGuid(), projectId, preset.Id, 150, 200, "Player C", "3", "one timer", DateTimeOffset.UtcNow);
        await repository.UpsertTagEventAsync(tagEvent, CancellationToken.None);

        var loaded = await repository.GetTagEventsAsync(projectId, new TagQuery(null, "Player C", null, null), CancellationToken.None);
        Assert.Single(loaded);
        Assert.Equal(150, loaded[0].StartFrame);
        Assert.Equal(200, loaded[0].EndFrame);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
