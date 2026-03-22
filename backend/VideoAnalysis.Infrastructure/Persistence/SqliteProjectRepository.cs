using Microsoft.Data.Sqlite;
using VideoAnalysis.Core.Abstractions;
using VideoAnalysis.Core.Enums;
using VideoAnalysis.Core.Models;

namespace VideoAnalysis.Infrastructure.Persistence;

public sealed class SqliteProjectRepository : IProjectRepository
{
    private const int SchemaVersion = 4;
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
        const string sql = """
                           SELECT id, project_id, name, color_hex, category, is_system, hotkey, icon_key, show_in_statistics
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
            reader.GetInt64(5) == 1,
            reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            reader.IsDBNull(7) ? "event" : reader.GetString(7),
            reader.IsDBNull(8) || reader.GetInt64(8) == 1));
    }

    public Task UpsertTagPresetAsync(TagPreset preset, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO TagPreset (
                               id, project_id, name, color_hex, category, is_system, hotkey, icon_key, show_in_statistics, created_at, updated_at)
                           VALUES (
                               $id, $project_id, $name, $color_hex, $category, $is_system, $hotkey, $icon_key, $show_in_statistics, $created_at, $updated_at)
                           ON CONFLICT(id) DO UPDATE SET
                               name = excluded.name,
                               color_hex = excluded.color_hex,
                               category = excluded.category,
                               is_system = excluded.is_system,
                               hotkey = excluded.hotkey,
                               icon_key = excluded.icon_key,
                               show_in_statistics = excluded.show_in_statistics,
                               updated_at = excluded.updated_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            command.Parameters.AddWithValue("$id", preset.Id.ToString());
            command.Parameters.AddWithValue("$project_id", preset.ProjectId.ToString());
            command.Parameters.AddWithValue("$name", preset.Name.Trim());
            command.Parameters.AddWithValue("$color_hex", preset.ColorHex.Trim());
            command.Parameters.AddWithValue("$category", string.IsNullOrWhiteSpace(preset.Category) ? "Custom" : preset.Category.Trim());
            command.Parameters.AddWithValue("$is_system", preset.IsSystem ? 1 : 0);
            command.Parameters.AddWithValue("$hotkey", NormalizeHotkey(preset.Hotkey));
            command.Parameters.AddWithValue("$icon_key", string.IsNullOrWhiteSpace(preset.IconKey) ? "event" : preset.IconKey.Trim());
            command.Parameters.AddWithValue("$show_in_statistics", preset.ShowInStatistics ? 1 : 0);
            command.Parameters.AddWithValue("$created_at", now);
            command.Parameters.AddWithValue("$updated_at", now);
        });
    }

    public Task DeleteTagPresetAsync(Guid projectId, Guid tagPresetId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM TagPreset WHERE project_id = $project_id AND id = $id;";
        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
            command.Parameters.AddWithValue("$id", tagPresetId.ToString());
        });
    }

    public async Task<IReadOnlyList<TagEvent>> GetTagEventsAsync(Guid projectId, TagQuery query, CancellationToken cancellationToken)
    {
        var sql = """
                  SELECT id, project_id, tag_preset_id, start_frame, end_frame, player, period, notes, created_at, team_side, is_open
                  FROM TagEvent
                  WHERE project_id = $project_id
                  """;

        var parameters = new List<(string Name, object Value)>
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
            parameters.Add(("$player", query.Player.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Period))
        {
            sql += " AND lower(period) = lower($period)";
            parameters.Add(("$period", query.Period.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            sql += " AND lower(coalesce(notes, '')) LIKE lower($notes)";
            parameters.Add(("$notes", $"%{query.Text.Trim()}%"));
        }

        if (query.TeamSide.HasValue)
        {
            sql += " AND team_side = $team_side";
            parameters.Add(("$team_side", (int)query.TeamSide.Value));
        }

        if (query.IsOpen.HasValue)
        {
            sql += " AND is_open = $is_open";
            parameters.Add(("$is_open", query.IsOpen.Value ? 1 : 0));
        }

        sql += " ORDER BY start_frame, end_frame, created_at;";

        return await QueryAsync(sql, cancellationToken, (command) =>
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
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
            DateTimeOffset.Parse(reader.GetString(8)),
            reader.IsDBNull(9) ? TeamSide.Unknown : (TeamSide)reader.GetInt32(9),
            !reader.IsDBNull(10) && reader.GetInt32(10) == 1));
    }

    public Task UpsertTagEventAsync(TagEvent tagEvent, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO TagEvent (
                               id, project_id, tag_preset_id, start_frame, end_frame, player, period, notes, created_at, team_side, is_open)
                           VALUES (
                               $id, $project_id, $tag_preset_id, $start_frame, $end_frame, $player, $period, $notes, $created_at, $team_side, $is_open)
                           ON CONFLICT(id) DO UPDATE SET
                               tag_preset_id = excluded.tag_preset_id,
                               start_frame = excluded.start_frame,
                               end_frame = excluded.end_frame,
                               player = excluded.player,
                               period = excluded.period,
                               notes = excluded.notes,
                               team_side = excluded.team_side,
                               is_open = excluded.is_open;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", tagEvent.Id.ToString());
            command.Parameters.AddWithValue("$project_id", tagEvent.ProjectId.ToString());
            command.Parameters.AddWithValue("$tag_preset_id", tagEvent.TagPresetId.ToString());
            command.Parameters.AddWithValue("$start_frame", tagEvent.StartFrame);
            command.Parameters.AddWithValue("$end_frame", tagEvent.EndFrame);
            command.Parameters.AddWithValue("$player", DbValue(tagEvent.Player));
            command.Parameters.AddWithValue("$period", DbValue(tagEvent.Period));
            command.Parameters.AddWithValue("$notes", DbValue(tagEvent.Notes));
            command.Parameters.AddWithValue("$created_at", tagEvent.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$team_side", (int)tagEvent.TeamSide);
            command.Parameters.AddWithValue("$is_open", tagEvent.IsOpen ? 1 : 0);
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

    public Task<IReadOnlyList<Playlist>> GetPlaylistsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, name, description, created_at, updated_at
                           FROM Playlist
                           WHERE project_id = $project_id
                           ORDER BY updated_at DESC, created_at DESC;
                           """;

        return QueryAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
        }, MapPlaylist);
    }

    public async Task<Playlist?> GetPlaylistAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, project_id, name, description, created_at, updated_at
                           FROM Playlist
                           WHERE project_id = $project_id AND id = $id
                           LIMIT 1;
                           """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$project_id", projectId.ToString());
        command.Parameters.AddWithValue("$id", playlistId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? MapPlaylist(reader) : null;
    }

    public Task UpsertPlaylistAsync(Playlist playlist, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO Playlist (
                               id, project_id, name, description, created_at, updated_at)
                           VALUES (
                               $id, $project_id, $name, $description, $created_at, $updated_at)
                           ON CONFLICT(id) DO UPDATE SET
                               name = excluded.name,
                               description = excluded.description,
                               updated_at = excluded.updated_at;
                           """;

        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$id", playlist.Id.ToString());
            command.Parameters.AddWithValue("$project_id", playlist.ProjectId.ToString());
            command.Parameters.AddWithValue("$name", playlist.Name.Trim());
            command.Parameters.AddWithValue("$description", DbValue(string.IsNullOrWhiteSpace(playlist.Description) ? null : playlist.Description.Trim()));
            command.Parameters.AddWithValue("$created_at", playlist.CreatedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$updated_at", playlist.UpdatedAtUtc.ToString("O"));
        });
    }

    public Task DeletePlaylistAsync(Guid projectId, Guid playlistId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM Playlist WHERE project_id = $project_id AND id = $id;";
        return ExecuteAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$project_id", projectId.ToString());
            command.Parameters.AddWithValue("$id", playlistId.ToString());
        });
    }

    public Task<IReadOnlyList<PlaylistItem>> GetPlaylistItemsAsync(Guid playlistId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, playlist_id, tag_event_id, tag_preset_id, sort_order, event_start_frame, event_end_frame,
                                  clip_start_frame, clip_end_frame, pre_roll_frames, post_roll_frames, label, player, team_side
                           FROM PlaylistItem
                           WHERE playlist_id = $playlist_id
                           ORDER BY sort_order ASC, clip_start_frame ASC;
                           """;

        return QueryAsync(sql, cancellationToken, (command) =>
        {
            command.Parameters.AddWithValue("$playlist_id", playlistId.ToString());
        }, MapPlaylistItem);
    }

    public async Task ReplacePlaylistItemsAsync(Guid playlistId, IReadOnlyList<PlaylistItem> items, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM PlaylistItem WHERE playlist_id = $playlist_id;";
                deleteCommand.Parameters.AddWithValue("$playlist_id", playlistId.ToString());
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string insertSql = """
                                     INSERT INTO PlaylistItem (
                                         id, playlist_id, tag_event_id, tag_preset_id, sort_order, event_start_frame, event_end_frame,
                                         clip_start_frame, clip_end_frame, pre_roll_frames, post_roll_frames, label, player, team_side)
                                     VALUES (
                                         $id, $playlist_id, $tag_event_id, $tag_preset_id, $sort_order, $event_start_frame, $event_end_frame,
                                         $clip_start_frame, $clip_end_frame, $pre_roll_frames, $post_roll_frames, $label, $player, $team_side);
                                     """;

            foreach (var item in items)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = insertSql;
                insertCommand.Parameters.AddWithValue("$id", item.Id.ToString());
                insertCommand.Parameters.AddWithValue("$playlist_id", item.PlaylistId.ToString());
                insertCommand.Parameters.AddWithValue("$tag_event_id", item.TagEventId.ToString());
                insertCommand.Parameters.AddWithValue("$tag_preset_id", item.TagPresetId.ToString());
                insertCommand.Parameters.AddWithValue("$sort_order", item.SortOrder);
                insertCommand.Parameters.AddWithValue("$event_start_frame", item.EventStartFrame);
                insertCommand.Parameters.AddWithValue("$event_end_frame", item.EventEndFrame);
                insertCommand.Parameters.AddWithValue("$clip_start_frame", item.ClipStartFrame);
                insertCommand.Parameters.AddWithValue("$clip_end_frame", item.ClipEndFrame);
                insertCommand.Parameters.AddWithValue("$pre_roll_frames", item.PreRollFrames);
                insertCommand.Parameters.AddWithValue("$post_roll_frames", item.PostRollFrames);
                insertCommand.Parameters.AddWithValue("$label", item.Label);
                insertCommand.Parameters.AddWithValue("$player", DbValue(item.Player));
                insertCommand.Parameters.AddWithValue("$team_side", (int)item.TeamSide);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var touchCommand = connection.CreateCommand();
            touchCommand.Transaction = transaction;
            touchCommand.CommandText = "UPDATE Playlist SET updated_at = $updated_at WHERE id = $playlist_id;";
            touchCommand.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            touchCommand.Parameters.AddWithValue("$playlist_id", playlistId.ToString());
            await touchCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

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

    private static string NormalizeHotkey(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? string.Empty : hotkey.Trim();

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

    private static Playlist MapPlaylist(SqliteDataReader reader)
    {
        return new Playlist(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            DateTimeOffset.Parse(reader.GetString(5)));
    }

    private static PlaylistItem MapPlaylistItem(SqliteDataReader reader)
    {
        return new PlaylistItem(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            Guid.Parse(reader.GetString(2)),
            Guid.Parse(reader.GetString(3)),
            reader.GetInt32(4),
            reader.GetInt64(5),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            reader.GetInt32(9),
            reader.GetInt32(10),
            reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? TeamSide.Unknown : (TeamSide)reader.GetInt32(13));
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
                           DROP TABLE IF EXISTS PlaylistItem;
                           DROP TABLE IF EXISTS Playlist;
                           DROP TABLE IF EXISTS TagEvent;
                           DROP TABLE IF EXISTS TagPreset;
                           DROP TABLE IF EXISTS MediaAsset;
                           DROP TABLE IF EXISTS ProjectVideo;
                           DROP TABLE IF EXISTS Project;
                           """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
        await EnsureTagPresetShowInStatisticsColumnAsync(connection, cancellationToken);
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

                           CREATE TABLE IF NOT EXISTS TagPreset (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL REFERENCES Project(id) ON DELETE CASCADE,
                               name TEXT NOT NULL,
                               color_hex TEXT NOT NULL,
                               category TEXT NOT NULL,
                               is_system INTEGER NOT NULL,
                               hotkey TEXT NOT NULL DEFAULT '',
                               icon_key TEXT NOT NULL DEFAULT 'event',
                               show_in_statistics INTEGER NOT NULL DEFAULT 1,
                               created_at TEXT NOT NULL,
                               updated_at TEXT NOT NULL
                           );

                           CREATE UNIQUE INDEX IF NOT EXISTS ux_tag_preset_hotkey_per_project
                           ON TagPreset(project_id, lower(hotkey))
                           WHERE length(trim(hotkey)) > 0;

                           CREATE TABLE IF NOT EXISTS TagEvent (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL REFERENCES Project(id) ON DELETE CASCADE,
                               tag_preset_id TEXT NOT NULL REFERENCES TagPreset(id) ON DELETE CASCADE,
                               start_frame INTEGER NOT NULL,
                               end_frame INTEGER NOT NULL,
                               player TEXT NULL,
                               period TEXT NULL,
                               notes TEXT NULL,
                               created_at TEXT NOT NULL,
                               team_side INTEGER NOT NULL,
                               is_open INTEGER NOT NULL
                           );

                           CREATE INDEX IF NOT EXISTS ix_tag_event_project_preset_open
                           ON TagEvent(project_id, tag_preset_id, is_open, start_frame);

                           CREATE TABLE IF NOT EXISTS Playlist (
                               id TEXT PRIMARY KEY,
                               project_id TEXT NOT NULL REFERENCES Project(id) ON DELETE CASCADE,
                               name TEXT NOT NULL,
                               description TEXT NULL,
                               created_at TEXT NOT NULL,
                               updated_at TEXT NOT NULL
                           );

                           CREATE INDEX IF NOT EXISTS ix_playlist_project_updated
                           ON Playlist(project_id, updated_at DESC, created_at DESC);

                           CREATE TABLE IF NOT EXISTS PlaylistItem (
                               id TEXT PRIMARY KEY,
                               playlist_id TEXT NOT NULL REFERENCES Playlist(id) ON DELETE CASCADE,
                               tag_event_id TEXT NOT NULL,
                               tag_preset_id TEXT NOT NULL,
                               sort_order INTEGER NOT NULL,
                               event_start_frame INTEGER NOT NULL,
                               event_end_frame INTEGER NOT NULL,
                               clip_start_frame INTEGER NOT NULL,
                               clip_end_frame INTEGER NOT NULL,
                               pre_roll_frames INTEGER NOT NULL,
                               post_roll_frames INTEGER NOT NULL,
                               label TEXT NOT NULL,
                               player TEXT NULL,
                               team_side INTEGER NOT NULL
                           );

                           CREATE INDEX IF NOT EXISTS ix_playlist_item_playlist_order
                           ON PlaylistItem(playlist_id, sort_order, clip_start_frame);

                           PRAGMA user_version = 4;
                           """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
        await EnsureTagPresetShowInStatisticsColumnAsync(connection, cancellationToken);
    }


    private static async Task EnsureTagPresetShowInStatisticsColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(TagPreset);";

        var hasColumn = false;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), "show_in_statistics", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (hasColumn)
        {
            return;
        }

        await ExecuteNonQueryAsync(
            connection,
            "ALTER TABLE TagPreset ADD COLUMN show_in_statistics INTEGER NOT NULL DEFAULT 1;",
            cancellationToken);
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


