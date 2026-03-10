using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Dtos;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Services;

public sealed class ProjectSetupService : IProjectSetupService
{
    private readonly IProjectRepository _repository;
    private readonly string _projectsRootPath;

    public ProjectSetupService(IProjectRepository repository, string projectsRootPath)
    {
        _repository = repository;
        _projectsRootPath = projectsRootPath;
        Directory.CreateDirectory(_projectsRootPath);
    }

    public async Task<CreateProjectResultDto> CreateProjectWithVideoAsync(
        CreateProjectRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SourceVideoPath))
        {
            throw new ArgumentException("Source video path is required.", nameof(request));
        }

        var sourceVideoPath = Path.GetFullPath(request.SourceVideoPath);
        if (!File.Exists(sourceVideoPath))
        {
            throw new FileNotFoundException("Source video file was not found.", sourceVideoPath);
        }

        await _repository.InitializeAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var projectFolderName = $"{SanitizeForPath(request.ProjectName)}-{projectId:N}";
        var projectFolderPath = Path.Combine(_projectsRootPath, projectFolderName);
        Directory.CreateDirectory(projectFolderPath);

        var originalFileName = Path.GetFileName(sourceVideoPath);
        var storedFileName = GetAvailableFileName(projectFolderPath, originalFileName);
        var storedVideoPath = Path.Combine(projectFolderPath, storedFileName);

        File.Copy(sourceVideoPath, storedVideoPath, overwrite: false);
        if (request.MoveVideoToProjectFolder)
        {
            File.Delete(sourceVideoPath);
        }

        var project = new Project(
            projectId,
            request.ProjectName.Trim(),
            now,
            now,
            Normalize(request.Description),
            Normalize(request.HomeTeamName),
            Normalize(request.AwayTeamName),
            projectFolderPath);

        var projectVideo = new ProjectVideo(
            Guid.NewGuid(),
            projectId,
            Normalize(request.VideoTitle) ?? Path.GetFileNameWithoutExtension(originalFileName),
            originalFileName,
            storedVideoPath,
            now);

        await _repository.CreateProjectAsync(project, cancellationToken);
        await _repository.UpsertProjectVideoAsync(projectVideo, cancellationToken);

        return new CreateProjectResultDto(
            project.Id,
            project.ProjectFolderPath,
            projectVideo.StoredFilePath,
            projectVideo.Title);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SanitizeForPath(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select((character) => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "project" : sanitized;
    }

    private static string GetAvailableFileName(string folderPath, string originalFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var extension = Path.GetExtension(originalFileName);
        var sanitizedBaseName = SanitizeForPath(baseName);
        var candidate = $"{sanitizedBaseName}{extension}";
        var counter = 1;

        while (File.Exists(Path.Combine(folderPath, candidate)))
        {
            candidate = $"{sanitizedBaseName}-{counter}{extension}";
            counter++;
        }

        return candidate;
    }
}
