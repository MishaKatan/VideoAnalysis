# Feature 03: Плейлисты из событий

## Цель

Собрать пользовательскую подборку клипов из уже размеченных событий:

1. Пользователь отмечает события на таймлайне.
2. Пользователь выбирает события, которые должны войти в подборку.
3. Frontend вызывает backend и создает `Playlist`.
4. Полученный плейлист можно:
   - воспроизводить во внутреннем плеере как набор сегментов;
   - позже экспортировать в один ролик через FFmpeg.

## Что добавлено в backend

Новые модели:

- `Playlist`
- `PlaylistItem`

Новые DTO:

- `CreatePlaylistRequestDto`
- `PlaylistSummaryDto`
- `PlaylistDetailsDto`
- `PlaylistItemDto`

Новый сервис:

- `IPlaylistService`
- `PlaylistService`

## Что хранится в БД

### Таблица `Playlist`

- `id`
- `project_id`
- `name`
- `description`
- `created_at`
- `updated_at`

### Таблица `PlaylistItem`

- `id`
- `playlist_id`
- `tag_event_id`
- `tag_preset_id`
- `sort_order`
- `event_start_frame`
- `event_end_frame`
- `clip_start_frame`
- `clip_end_frame`
- `pre_roll_frames`
- `post_roll_frames`
- `label`
- `player`
- `team_side`

`PlaylistItem` хранит уже готовый снимок сегмента. Это важно: если исходный `TagEvent` позже изменится, плейлист остается стабильным.

## Backend API для frontend

### Создать плейлист

```csharp
var playlist = await _playlistService.CreatePlaylistAsync(
    new CreatePlaylistRequestDto(
        projectId,
        "Все голы",
        selectedEventIds,
        PreRollFrames: 15,
        PostRollFrames: 25,
        Description: "Голы матча",
        MaxFrame: durationFrames),
    CancellationToken.None);
```

### Получить список плейлистов проекта

```csharp
var playlists = await _playlistService.GetPlaylistsAsync(projectId, CancellationToken.None);
```

### Получить один плейлист с элементами

```csharp
var playlist = await _playlistService.GetPlaylistAsync(projectId, playlistId, CancellationToken.None);
```

### Получить сегменты для внутреннего плеера или экспорта

```csharp
var segments = await _playlistService.GetClipSegmentsAsync(projectId, playlistId, CancellationToken.None);
```

### Удалить плейлист

```csharp
await _playlistService.DeletePlaylistAsync(projectId, playlistId, CancellationToken.None);
```

## Правила backend

- В плейлист можно добавлять только закрытые события (`IsOpen = false`).
- События внутри плейлиста сортируются по времени начала.
- `clip_start_frame = max(0, start_frame - pre_roll)`.
- `clip_end_frame = end_frame + post_roll`.
- Если frontend знает длину видео, он должен передать `MaxFrame`, чтобы backend обрезал конец сегмента по длительности файла.

## Что нужно сделать на frontend

1. Добавить у события действие `+` или `Добавить в подборку`.
2. Хранить локальный список выбранных `TagEventId`.
3. На кнопке `Создать плейлист` вызвать `CreatePlaylistAsync`.
4. Для внутреннего плеера получать сегменты через `GetClipSegmentsAsync`.
5. Для экрана списка плейлистов использовать `GetPlaylistsAsync`.

## Definition of Done

1. Пользователь может выбрать несколько закрытых событий.
2. Пользователь может создать плейлист с `name + pre-roll + post-roll`.
3. Backend сохраняет `Playlist` и `PlaylistItem`.
4. Frontend получает список готовых сегментов для воспроизведения.
