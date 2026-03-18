namespace VideoAnalysis.Core.Abstractions;

public interface IProjectRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task CreateProjectAsync(Models.Project project, CancellationToken cancellationToken);
    Task<Models.Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Models.Project>> ListProjectsAsync(CancellationToken cancellationToken);

    Task UpsertProjectVideoAsync(Models.ProjectVideo projectVideo, CancellationToken cancellationToken);
    Task<Models.ProjectVideo?> GetProjectVideoAsync(Guid projectId, CancellationToken cancellationToken);

    Task UpsertMediaAssetAsync(Models.MediaAsset mediaAsset, CancellationToken cancellationToken);
    Task<Models.MediaAsset?> GetMediaAssetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Models.TagPreset>> GetTagPresetsAsync(Guid projectId, CancellationToken cancellationToken);
    Task UpsertTagPresetAsync(Models.TagPreset preset, CancellationToken cancellationToken);
    Task DeleteTagPresetAsync(Guid projectId, Guid tagPresetId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Models.TagEvent>> GetTagEventsAsync(Guid projectId, Models.TagQuery query, CancellationToken cancellationToken);
    Task UpsertTagEventAsync(Models.TagEvent tagEvent, CancellationToken cancellationToken);
    Task DeleteTagEventAsync(Guid projectId, Guid tagEventId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Models.Annotation>> GetAnnotationsAsync(Guid projectId, Models.FrameRange range, CancellationToken cancellationToken);
    Task UpsertAnnotationAsync(Models.Annotation annotation, CancellationToken cancellationToken);
    Task DeleteAnnotationAsync(Guid projectId, Guid annotationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Models.ClipRecipe>> GetClipRecipesAsync(Guid projectId, CancellationToken cancellationToken);
    Task UpsertClipRecipeAsync(Models.ClipRecipe recipe, CancellationToken cancellationToken);

    Task<IReadOnlyList<Models.ExportJob>> GetExportJobsAsync(Guid projectId, CancellationToken cancellationToken);
    Task UpsertExportJobAsync(Models.ExportJob exportJob, CancellationToken cancellationToken);
}
