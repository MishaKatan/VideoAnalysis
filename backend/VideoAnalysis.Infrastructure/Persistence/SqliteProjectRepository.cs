using Microsoft.Data.Sqlite;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Persistence;

public sealed class SqliteProjectRepository : IProjectRepository
{
    private const int SchemaVersion = 2;
    private readonly string _connectionString;

    public SqliteProjectRepository(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);

        var currentVersion = await GetSchemaVersionAsync(connection, cancellationToken);
        if (currentVersion != SchemaVersion)
        {
            await ResetSchemaAsync(connection, cancellationToken);
        }

        await EnsureCurrentSchemaAsync(connection, cancellationToken);
    }

    public Task CreateProjectAsync(Project project, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO Project (
                               id, name, description, home_team_name, away_team_name, project_folder_path, created_at, updated_at)
                           VALUES (
                               $id, $name, $description, $home_team_name, $away_team_name, $project_folder_path, $created_at, $updated_at)
                           ON CONFLICT(id) DO UPDATE SET
                               name = excluded.name,
                               description = excluded.description,
                               home_team_name = excluded.home_team_name,
                               away_team_name = excluded.away_team_name,
                               project_folder_path = excluded.project_folder_path,
                               updated_at = excluded.updated_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", project.Id.ToString());
            command.Parameters.AddWithValue("$name", project.Name);
            command.Parameters.AddWithValue("$description", DbValue(project.Description));
            command.Parameters.AddWithValue("$home_team_name", DbValue(project.HomeTeamName));
            command.Parameters.AddWithValue("$away_team_name", DbValue(project.AwayTeamName));
            command.Parameters.AddWithValue("$project_folder_path", project.ProjectFolderPath);
            command.Parameters.AddWithValue("$created_at", project.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$updated_at", project.UpdatedAtUtc.ToString("O"));
        });
    }

    public async Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, name, description, home_team_name, away_team_name, project_folder_path, created_at, updated_at
                           FROM Project
                           WHERE id = $id
                           LIMIT 1;
                           """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? MapProject(reader) : null;
    }

    public Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, name, description, home_team_name, away_team_name, project_folder_path, created_at, updated_at
                           FROM Project
                           ORDER BY updated_at DESC;
                           """;

        return QueryAsync(sql, cancellationToken, MapProject);
    }

    public Task UpsertProjectVideoAsync(ProjectVideo projectVideo, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO ProjectVideo (
                               id, project_id, title, original_file_name, stored_file_path, imported_at)
                           VALUES (
                               $id, $project_id, $title, $original_file_name, $stored_file_path, $imported_at)
                           ON CONFLICT(project_id) DO UPDATE SET
                               id = excluded.id,
                               title = excluded.title,
                               original_file_name = excluded.original_file_name,
                               stored_file_path = excluded.stored_file_path,
                               imported_at = excluded.imported_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", projectVideo.Id.ToString());
            command.Parameters.AddWithValue("$project_id", projectVideo.ProjectId.ToString());
            command.Parameters.AddWithValue("$title", projectVideo.Title);
            command.Parameters.AddWithValue("$original_file_name", projectVideo.OriginalFileName);
            command.Parameters.AddWithValue("$stored_file_path", projectVideo.StoredFilePath);
            command.Parameters.AddWithValue("$imported_at", projectVideo.ImportedAtUtc.ToString("O"));
        });
    }

    public async Task<ProjectVideo?> GetProjectVideoAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, title, original_file_name, stored_file_path, imported_at
                           FROM ProjectVideo
                           WHERE project_id = $project_id
                           LIMIT 1;
                           """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$project_id", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? MapProjectVideo(reader) : null;
    }

    public Task UpsertMediaAssetAsync(MediaAsset mediaAsset, CancellationToken cancellationToken)
    {
        var projectVideo = new ProjectVideo(
            mediaAsset.Id,
            mediaAsset.ProjectId,
            Path.GetFileNameWithoutExtension(mediaAsset.FilePath),
            Path.GetFileName(mediaAsset.FilePath),
            mediaAsset.FilePath,
            mediaAsset.ImportedAtUtc);

        return UpsertProjectVideoAsync(projectVideo, cancellationToken);
    }

    public async Task<MediaAsset?> GetMediaAssetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var projectVideo = await GetProjectVideoAsync(projectId, cancellationToken);
        if (projectVideo is null)
        {
            return null;
        }

        return new MediaAsset(
            projectVideo.Id,
            projectVideo.ProjectId,
            projectVideo.StoredFilePath,
            0,
            0,
            0,
            0,
            projectVideo.ImportedAtUtc);
    }

    public Task<IReadOnlyList<TagPreset>> GetTagPresetsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TagPreset>>([]);
    }

    public Task UpsertTagPresetAsync(TagPreset preset, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<TagEvent>> GetTagEventsAsync(Guid projectId, TagQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<TagEvent>>([]);
    }

    public Task UpsertTagEventAsync(TagEvent tagEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteTagEventAsync(Guid projectId, Guid tagEventId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<Annotation>> GetAnnotationsAsync(Guid projectId, FrameRange range, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<Annotation>>([]);
    }

    public Task UpsertAnnotationAsync(Annotation annotation, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteAnnotationAsync(Guid projectId, Guid annotationId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<ClipRecipe>> GetClipRecipesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ClipRecipe>>([]);
    }

    public Task UpsertClipRecipeAsync(ClipRecipe recipe, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<ExportJob>> GetExportJobsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ExportJob>>([]);
    }

    public Task UpsertExportJobAsync(ExportJob exportJob, CancellationToken cancellationToken) => Task.CompletedTask;

    private SqliteConnection CreateConnection() => new(_connectionString);

    private static object DbValue(string? value) => value is null ? DBNull.Value : value;

    private static Project MapProject(SqliteDataReader reader)
    {
        return new Project(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(6)),
            DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetString(5));
    }

    private static ProjectVideo MapProjectVideo(SqliteDataReader reader)
    {
        return new ProjectVideo(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5)));
    }

    private async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is long number ? (int)number : 0;
    }

    private async Task ResetSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
                           DROP TABLE IF EXISTS ExportJob;
                           DROP TABLE IF EXISTS ClipRecipe;
                           DROP TABLE IF EXISTS Annotation;
                           DROP TABLE IF EXISTS TagEvent;
                           DROP TABLE IF EXISTS TagPreset;
                           DROP TABLE IF EXISTS MediaAsset;
                           DROP TABLE IF EXISTS ProjectVideo;
                           DROP TABLE IF EXISTS Project;
                           """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
    }

    private async Task EnsureCurrentSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS Project (
                               id TEXT PRIMARY KEY,
                               name TEXT NOT NULL,
                               description TEXT NULL,
                               home_team_name TEXT NULL,
                               away_team_name TEXT NULL,
                               project_folder_path TEXT NOT NULL,
                               created_at TEXT NOT NULL,
                               updated_at TEXT NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS ProjectVideo (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL UNIQUE REFERENCES Project(id) ON DELETE CASCADE,
                               title TEXT NOT NULL,
                               original_file_name TEXT NOT NULL,
                               stored_file_path TEXT NOT NULL,
                               imported_at TEXT NOT NULL
                           );
                           PRAGMA user_version = 2;
                           """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
    }

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken, Action<SqliteCommand> bind)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        Func<SqliteDataReader, T> map)
    {
        return QueryAsync(sql, cancellationToken, _ => { }, map);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        Action<SqliteCommand> bind,
        Func<SqliteDataReader, T> map)
    {
        var items = new List<T>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }

        return items;
    }
}
