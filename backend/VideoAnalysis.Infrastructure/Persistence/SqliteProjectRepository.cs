using Microsoft.Data.Sqlite;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Persistence;

public sealed class SqliteProjectRepository : IProjectRepository
{
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
        const string sql = """
                           PRAGMA foreign_keys = ON;
                           CREATE TABLE IF NOT EXISTS Project (
                               id TEXT PRIMARY KEY,
                               name TEXT NOT NULL,
                               description TEXT NULL,
                               created_at TEXT NOT NULL,
                               updated_at TEXT NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS MediaAsset (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL UNIQUE,
                               file_path TEXT NOT NULL,
                               fps REAL NOT NULL,
                               duration_frames INTEGER NOT NULL,
                               width INTEGER NOT NULL,
                               height INTEGER NOT NULL,
                               imported_at TEXT NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS TagPreset (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL,
                               name TEXT NOT NULL,
                               color_hex TEXT NOT NULL,
                               category TEXT NOT NULL,
                               is_system INTEGER NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS TagEvent (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL,
                               tag_preset_id TEXT NOT NULL,
                               start_frame INTEGER NOT NULL,
                               end_frame INTEGER NOT NULL,
                               player TEXT NULL,
                               period TEXT NULL,
                               notes TEXT NULL,
                               created_at TEXT NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS Annotation (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL,
                               tag_event_id TEXT NULL,
                               start_frame INTEGER NOT NULL,
                               end_frame INTEGER NOT NULL,
                               shape_type INTEGER NOT NULL,
                               x1 REAL NOT NULL,
                               y1 REAL NOT NULL,
                               x2 REAL NOT NULL,
                               y2 REAL NOT NULL,
                               text TEXT NULL,
                               color_hex TEXT NOT NULL,
                               stroke_width REAL NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS ClipRecipe (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL,
                               name TEXT NOT NULL,
                               tag_preset_id TEXT NULL,
                               player TEXT NULL,
                               period TEXT NULL,
                               query_text TEXT NULL,
                               pre_roll_frames INTEGER NOT NULL,
                               post_roll_frames INTEGER NOT NULL,
                               created_at TEXT NOT NULL
                           );
                           CREATE TABLE IF NOT EXISTS ExportJob (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL,
                               clip_recipe_id TEXT NULL,
                               destination INTEGER NOT NULL,
                               output_path TEXT NOT NULL,
                               remote_object_key TEXT NULL,
                               status INTEGER NOT NULL,
                               error TEXT NULL,
                               created_at TEXT NOT NULL,
                               completed_at TEXT NULL
                           );
                           """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task CreateProjectAsync(Project project, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO Project (id, name, description, created_at, updated_at)
                           VALUES ($id, $name, $description, $created_at, $updated_at)
                           ON CONFLICT(id) DO UPDATE SET
                               name = excluded.name,
                               description = excluded.description,
                               updated_at = excluded.updated_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", project.Id.ToString());
            command.Parameters.AddWithValue("$name", project.Name);
            command.Parameters.AddWithValue("$description", (object?)project.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("$created_at", project.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$updated_at", project.UpdatedAtUtc.ToString("O"));
        });
    }

    public async Task<Project?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, description, created_at, updated_at FROM Project WHERE id = $id LIMIT 1;";
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Project(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    public async Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, description, created_at, updated_at FROM Project ORDER BY updated_at DESC;";
        return await QueryAsync(sql, cancellationToken, (reader) => new Project(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(2) ? null : reader.GetString(2)));
    }

    public Task UpsertMediaAssetAsync(MediaAsset mediaAsset, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO MediaAsset (
                               id, project_id, file_path, fps, duration_frames, width, height, imported_at)
                           VALUES (
                               $id, $project_id, $file_path, $fps, $duration_frames, $width, $height, $imported_at)
                           ON CONFLICT(project_id) DO UPDATE SET
                               id = excluded.id,
                               file_path = excluded.file_path,
                               fps = excluded.fps,
                               duration_frames = excluded.duration_frames,
                               width = excluded.width,
                               height = excluded.height,
                               imported_at = excluded.imported_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", mediaAsset.Id.ToString());
            command.Parameters.AddWithValue("$project_id", mediaAsset.ProjectId.ToString());
            command.Parameters.AddWithValue("$file_path", mediaAsset.FilePath);
            command.Parameters.AddWithValue("$fps", mediaAsset.FramesPerSecond);
            command.Parameters.AddWithValue("$duration_frames", mediaAsset.DurationFrames);
            command.Parameters.AddWithValue("$width", mediaAsset.Width);
            command.Parameters.AddWithValue("$height", mediaAsset.Height);
            command.Parameters.AddWithValue("$imported_at", mediaAsset.ImportedAtUtc.ToString("O"));
        });
    }

    public async Task<MediaAsset?> GetMediaAssetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, file_path, fps, duration_frames, width, height, imported_at
                           FROM MediaAsset
                           WHERE project_id = $project_id
                           LIMIT 1;
                           """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$project_id", projectId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new MediaAsset(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetDouble(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            DateTimeOffset.Parse(reader.GetString(7)));
    }

    public Task<IReadOnlyList<TagPreset>> GetTagPresetsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, name, color_hex, category, is_system
                           FROM TagPreset
                           WHERE project_id = $project_id
                           ORDER BY is_system DESC, name ASC;
                           """;

        return QueryAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
        }, (reader) => new TagPreset(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5) == 1));
    }

    public Task UpsertTagPresetAsync(TagPreset preset, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO TagPreset (id, project_id, name, color_hex, category, is_system)
                           VALUES ($id, $project_id, $name, $color_hex, $category, $is_system)
                           ON CONFLICT(id) DO UPDATE SET
                               name = excluded.name,
                               color_hex = excluded.color_hex,
                               category = excluded.category,
                               is_system = excluded.is_system;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", preset.Id.ToString());
            command.Parameters.AddWithValue("$project_id", preset.ProjectId.ToString());
            command.Parameters.AddWithValue("$name", preset.Name);
            command.Parameters.AddWithValue("$color_hex", preset.ColorHex);
            command.Parameters.AddWithValue("$category", preset.Category);
            command.Parameters.AddWithValue("$is_system", preset.IsSystem ? 1 : 0);
        });
    }

    public async Task<IReadOnlyList<TagEvent>> GetTagEventsAsync(Guid projectId, TagQuery query, CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT id, project_id, tag_preset_id, start_frame, end_frame, player, period, notes, created_at
                  FROM TagEvent
                  WHERE project_id = $project_id
                  """;
        var parameters = new List<(string Name, object? Value)>
        {
            ("$project_id", projectId.ToString())
        };

        if (query.TagPresetId.HasValue)
        {
            sql += " AND tag_preset_id = $tag_preset_id";
            parameters.Add(("$tag_preset_id", query.TagPresetId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(query.Player))
        {
            sql += " AND lower(player) = lower($player)";
            parameters.Add(("$player", query.Player));
        }

        if (!string.IsNullOrWhiteSpace(query.Period))
        {
            sql += " AND lower(period) = lower($period)";
            parameters.Add(("$period", query.Period));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            sql += " AND lower(coalesce(notes, '')) LIKE lower($notes)";
            parameters.Add(("$notes", $"%{query.Text}%"));
        }

        sql += " ORDER BY start_frame, end_frame;";

        return await QueryAsync(sql, cancellationToken, (command) =>
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
        }, (reader) => new TagEvent(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            Guid.Parse(reader.GetString(2)),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            DateTimeOffset.Parse(reader.GetString(8))));
    }

    public Task UpsertTagEventAsync(TagEvent tagEvent, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO TagEvent (
                               id, project_id, tag_preset_id, start_frame, end_frame, player, period, notes, created_at)
                           VALUES (
                               $id, $project_id, $tag_preset_id, $start_frame, $end_frame, $player, $period, $notes, $created_at)
                           ON CONFLICT(id) DO UPDATE SET
                               tag_preset_id = excluded.tag_preset_id,
                               start_frame = excluded.start_frame,
                               end_frame = excluded.end_frame,
                               player = excluded.player,
                               period = excluded.period,
                               notes = excluded.notes;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", tagEvent.Id.ToString());
            command.Parameters.AddWithValue("$project_id", tagEvent.ProjectId.ToString());
            command.Parameters.AddWithValue("$tag_preset_id", tagEvent.TagPresetId.ToString());
            command.Parameters.AddWithValue("$start_frame", tagEvent.StartFrame);
            command.Parameters.AddWithValue("$end_frame", tagEvent.EndFrame);
            command.Parameters.AddWithValue("$player", (object?)tagEvent.Player ?? DBNull.Value);
            command.Parameters.AddWithValue("$period", (object?)tagEvent.Period ?? DBNull.Value);
            command.Parameters.AddWithValue("$notes", (object?)tagEvent.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("$created_at", tagEvent.CreatedAtUtc.ToString("O"));
        });
    }

    public Task DeleteTagEventAsync(Guid projectId, Guid tagEventId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM TagEvent WHERE project_id = $project_id AND id = $id;";
        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
            command.Parameters.AddWithValue("$id", tagEventId.ToString());
        });
    }

    public Task<IReadOnlyList<Annotation>> GetAnnotationsAsync(Guid projectId, FrameRange range, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, tag_event_id, start_frame, end_frame, shape_type, x1, y1, x2, y2, text, color_hex, stroke_width
                           FROM Annotation
                           WHERE project_id = $project_id
                             AND start_frame <= $end_frame
                             AND end_frame >= $start_frame
                           ORDER BY start_frame, end_frame;
                           """;

        return QueryAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
            command.Parameters.AddWithValue("$start_frame", range.StartFrame);
            command.Parameters.AddWithValue("$end_frame", range.EndFrame);
        }, (reader) => new Annotation(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            reader.GetInt64(3),
            reader.GetInt64(4),
            (AnnotationShapeType)reader.GetInt32(5),
            reader.GetDouble(6),
            reader.GetDouble(7),
            reader.GetDouble(8),
            reader.GetDouble(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetString(11),
            reader.GetDouble(12)));
    }

    public Task UpsertAnnotationAsync(Annotation annotation, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO Annotation (
                               id, project_id, tag_event_id, start_frame, end_frame, shape_type, x1, y1, x2, y2, text, color_hex, stroke_width)
                           VALUES (
                               $id, $project_id, $tag_event_id, $start_frame, $end_frame, $shape_type, $x1, $y1, $x2, $y2, $text, $color_hex, $stroke_width)
                           ON CONFLICT(id) DO UPDATE SET
                               tag_event_id = excluded.tag_event_id,
                               start_frame = excluded.start_frame,
                               end_frame = excluded.end_frame,
                               shape_type = excluded.shape_type,
                               x1 = excluded.x1,
                               y1 = excluded.y1,
                               x2 = excluded.x2,
                               y2 = excluded.y2,
                               text = excluded.text,
                               color_hex = excluded.color_hex,
                               stroke_width = excluded.stroke_width;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", annotation.Id.ToString());
            command.Parameters.AddWithValue("$project_id", annotation.ProjectId.ToString());
            command.Parameters.AddWithValue("$tag_event_id", annotation.TagEventId.HasValue ? annotation.TagEventId.Value.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$start_frame", annotation.StartFrame);
            command.Parameters.AddWithValue("$end_frame", annotation.EndFrame);
            command.Parameters.AddWithValue("$shape_type", (int)annotation.ShapeType);
            command.Parameters.AddWithValue("$x1", annotation.X1);
            command.Parameters.AddWithValue("$y1", annotation.Y1);
            command.Parameters.AddWithValue("$x2", annotation.X2);
            command.Parameters.AddWithValue("$y2", annotation.Y2);
            command.Parameters.AddWithValue("$text", (object?)annotation.Text ?? DBNull.Value);
            command.Parameters.AddWithValue("$color_hex", annotation.ColorHex);
            command.Parameters.AddWithValue("$stroke_width", annotation.StrokeWidth);
        });
    }

    public Task DeleteAnnotationAsync(Guid projectId, Guid annotationId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM Annotation WHERE project_id = $project_id AND id = $id;";
        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
            command.Parameters.AddWithValue("$id", annotationId.ToString());
        });
    }

    public Task<IReadOnlyList<ClipRecipe>> GetClipRecipesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, name, tag_preset_id, player, period, query_text, pre_roll_frames, post_roll_frames, created_at
                           FROM ClipRecipe
                           WHERE project_id = $project_id
                           ORDER BY created_at;
                           """;

        return QueryAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
        }, (reader) => new ClipRecipe(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            DateTimeOffset.Parse(reader.GetString(9))));
    }

    public Task UpsertClipRecipeAsync(ClipRecipe recipe, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO ClipRecipe (
                               id, project_id, name, tag_preset_id, player, period, query_text, pre_roll_frames, post_roll_frames, created_at)
                           VALUES (
                               $id, $project_id, $name, $tag_preset_id, $player, $period, $query_text, $pre_roll_frames, $post_roll_frames, $created_at)
                           ON CONFLICT(id) DO UPDATE SET
                               name = excluded.name,
                               tag_preset_id = excluded.tag_preset_id,
                               player = excluded.player,
                               period = excluded.period,
                               query_text = excluded.query_text,
                               pre_roll_frames = excluded.pre_roll_frames,
                               post_roll_frames = excluded.post_roll_frames;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", recipe.Id.ToString());
            command.Parameters.AddWithValue("$project_id", recipe.ProjectId.ToString());
            command.Parameters.AddWithValue("$name", recipe.Name);
            command.Parameters.AddWithValue("$tag_preset_id", recipe.TagPresetId.HasValue ? recipe.TagPresetId.Value.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$player", (object?)recipe.Player ?? DBNull.Value);
            command.Parameters.AddWithValue("$period", (object?)recipe.Period ?? DBNull.Value);
            command.Parameters.AddWithValue("$query_text", (object?)recipe.QueryText ?? DBNull.Value);
            command.Parameters.AddWithValue("$pre_roll_frames", recipe.PreRollFrames);
            command.Parameters.AddWithValue("$post_roll_frames", recipe.PostRollFrames);
            command.Parameters.AddWithValue("$created_at", recipe.CreatedAtUtc.ToString("O"));
        });
    }

    public Task<IReadOnlyList<ExportJob>> GetExportJobsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, clip_recipe_id, destination, output_path, remote_object_key, status, error, created_at, completed_at
                           FROM ExportJob
                           WHERE project_id = $project_id
                           ORDER BY created_at DESC;
                           """;

        return QueryAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
        }, (reader) => new ExportJob(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            (ExportDestinationType)reader.GetInt32(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            (ExportJobStatus)reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            DateTimeOffset.Parse(reader.GetString(8)),
            reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))));
    }

    public Task UpsertExportJobAsync(ExportJob exportJob, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO ExportJob (
                               id, project_id, clip_recipe_id, destination, output_path, remote_object_key, status, error, created_at, completed_at)
                           VALUES (
                               $id, $project_id, $clip_recipe_id, $destination, $output_path, $remote_object_key, $status, $error, $created_at, $completed_at)
                           ON CONFLICT(id) DO UPDATE SET
                               clip_recipe_id = excluded.clip_recipe_id,
                               destination = excluded.destination,
                               output_path = excluded.output_path,
                               remote_object_key = excluded.remote_object_key,
                               status = excluded.status,
                               error = excluded.error,
                               completed_at = excluded.completed_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", exportJob.Id.ToString());
            command.Parameters.AddWithValue("$project_id", exportJob.ProjectId.ToString());
            command.Parameters.AddWithValue("$clip_recipe_id", exportJob.ClipRecipeId.HasValue ? exportJob.ClipRecipeId.Value.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$destination", (int)exportJob.Destination);
            command.Parameters.AddWithValue("$output_path", exportJob.OutputPath);
            command.Parameters.AddWithValue("$remote_object_key", (object?)exportJob.RemoteObjectKey ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", (int)exportJob.Status);
            command.Parameters.AddWithValue("$error", (object?)exportJob.Error ?? DBNull.Value);
            command.Parameters.AddWithValue("$created_at", exportJob.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$completed_at", exportJob.CompletedAtUtc.HasValue ? exportJob.CompletedAtUtc.Value.ToString("O") : DBNull.Value);
        });
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken, Action<SqliteCommand> bind)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);
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
