using VideoAnalysis.Core.Models;
using VideoAnalysis.Infrastructure.Persistence;
using VideoAnalysis.Infrastructure.Services;
using VideoAnalysis.Core.Dtos;

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

        var project = new Project(
            Guid.NewGuid(),
            "Test Match",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "Quarter-final",
            "Avto",
            "Ak Bars",
            "C:\\Projects\\test-match");

        await repository.CreateProjectAsync(project, CancellationToken.None);

        var loaded = await repository.GetProjectAsync(project.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(project.Name, loaded!.Name);
        Assert.Equal(project.Description, loaded.Description);
        Assert.Equal(project.HomeTeamName, loaded.HomeTeamName);
        Assert.Equal(project.AwayTeamName, loaded.AwayTeamName);
        Assert.Equal(project.ProjectFolderPath, loaded.ProjectFolderPath);
    }

    [Fact]
    public async Task UpsertAndLoadProjectVideo_Works()
    {
        var repository = new SqliteProjectRepository(_dbPath);
        await repository.InitializeAsync(CancellationToken.None);

        var projectId = Guid.NewGuid();
        await repository.CreateProjectAsync(
            new Project(projectId, "Match", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null, "C:\\Projects\\match"),
            CancellationToken.None);

        var video = new ProjectVideo(
            Guid.NewGuid(),
            projectId,
            "Game 1",
            "game1.mp4",
            "C:\\Projects\\match\\game1.mp4",
            DateTimeOffset.UtcNow);

        await repository.UpsertProjectVideoAsync(video, CancellationToken.None);

        var loaded = await repository.GetProjectVideoAsync(projectId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(video.Title, loaded!.Title);
        Assert.Equal(video.StoredFilePath, loaded.StoredFilePath);
    }

    [Fact]
    public async Task CreateProjectWithVideo_MovesVideoIntoProjectFolder()
    {
        var repository = new SqliteProjectRepository(_dbPath);
        var projectsRootPath = Path.Combine(Path.GetTempPath(), "video-analysis-tests", "projects", Guid.NewGuid().ToString("N"));
        var sourceFolder = Path.Combine(Path.GetTempPath(), "video-analysis-tests", "source", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectsRootPath);

        var sourceVideoPath = Path.Combine(sourceFolder, "match.mp4");
        await File.WriteAllTextAsync(sourceVideoPath, "video", CancellationToken.None);

        try
        {
            var service = new ProjectSetupService(repository, projectsRootPath);
            var result = await service.CreateProjectWithVideoAsync(
                new CreateProjectRequestDto(
                    "Playoffs",
                    sourceVideoPath,
                    Description: "Round 1",
                    HomeTeamName: "Home",
                    AwayTeamName: "Away"),
                CancellationToken.None);

            Assert.False(File.Exists(sourceVideoPath));
            Assert.True(File.Exists(result.StoredVideoPath));

            var project = await repository.GetProjectAsync(result.ProjectId, CancellationToken.None);
            var video = await repository.GetProjectVideoAsync(result.ProjectId, CancellationToken.None);

            Assert.NotNull(project);
            Assert.NotNull(video);
            Assert.Equal("Home", project!.HomeTeamName);
            Assert.Equal("Away", project.AwayTeamName);
            Assert.Equal("match.mp4", video!.OriginalFileName);
            Assert.StartsWith(projectsRootPath, result.ProjectFolderPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(projectsRootPath))
            {
                Directory.Delete(projectsRootPath, recursive: true);
            }

            if (Directory.Exists(sourceFolder))
            {
                Directory.Delete(sourceFolder, recursive: true);
            }
        }
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
