# Feature 02: Типы событий и фиксация таймкодов по hotkey

## Цель

С фронта:

1. Создавать типы событий внутри проекта.
2. Фиксировать события по двойному нажатию hotkey:
   - первое нажатие: start
   - второе нажатие: end

Сохранять таймкоды в `frame`.

## Параметры типа события (Event Type)

Модель:

- `TagPreset`

Файл:

- `backend/VideoAnalysis.Core/Models/TagPreset.cs`

Поля:

- `Name`
- `Hotkey`
- `IconKey`
- `ColorHex` (`#RRGGBB`)

Ограничение:

- hotkey уникален в рамках проекта (case-insensitive).

Реализация ограничения:

- уникальный индекс `ux_tag_preset_hotkey_per_project`
- `backend/VideoAnalysis.Infrastructure/Persistence/SqliteProjectRepository.cs`

## Параметры события (Tag Event)

Модель:

- `TagEvent`

Файл:

- `backend/VideoAnalysis.Core/Models/TagEvent.cs`

Ключевые поля:

- `TagPresetId`
- `StartFrame`
- `EndFrame`
- `TeamSide` (`Unknown/Home/Away/Neutral`)
- `IsOpen` (`true` пока событие не закрыто)

## Важное правило по TeamSide

Согласовано: `Вариант A`.

- команда фиксируется на первом нажатии (в момент открытия)
- второе нажатие закрывает интервал времени
- команда не перезаписывается при закрытии
- исключение: если открытое событие имело `Unknown`, при закрытии можно записать выбранную сторону

Реализация:

- `backend/VideoAnalysis.Infrastructure/Services/EventCaptureService.cs`

## Hotkey normalization (контракт backend <-> frontend)

Текущее поведение backend:

- `trim` строки hotkey
- сравнение hotkey case-insensitive
- уникальность в БД case-insensitive (`lower(hotkey)`)

Рекомендация для фронта:

1. Перед сохранением и перед отправкой на capture:
   - trim
   - схлопнуть лишние пробелы
   - использовать единую форму (например `Ctrl+Shift+G`)
2. Отображать пользователю уже нормализованную строку.

Причина:

- меньше коллизий формата (`Ctrl+G`, `ctrl+g`, `Ctrl + G`).

## Backend контракты (v1)

Создание/обновление типа события:

- `IProjectRepository.UpsertTagPresetAsync(...)`

Чтение типов событий:

- `IProjectRepository.GetTagPresetsAsync(...)`

Фиксация события по hotkey:

- `IEventCaptureService.RegisterHotkeyPressAsync(projectId, hotkey, frame, teamSide, ct)`

Чтение/редактирование/удаление событий:

- `IProjectRepository.GetTagEventsAsync(...)`
- `IProjectRepository.UpsertTagEventAsync(...)`
- `IProjectRepository.DeleteTagEventAsync(...)`

## Сценарий двойного нажатия

Первое нажатие hotkey:

- ищется `TagPreset` по hotkey
- ищется открытое событие (`IsOpen = true`) для этого типа
- если открытого нет: создается новое событие с `StartFrame = EndFrame = currentFrame`, `IsOpen = true`

Второе нажатие той же hotkey:

- закрывается последнее открытое событие этого типа
- `EndFrame` ставится в текущий frame
- `IsOpen = false`

## Интеграция с frontend

### 1) Создание типа события

Форма должна отправлять:

- `name`
- `hotkey`
- `iconKey`
- `colorHex`

Пример:

```csharp
await _repository.UpsertTagPresetAsync(
    new TagPreset(
        Guid.NewGuid(),
        projectId,
        EventTypeName,
        EventTypeColorHex,
        "Custom",
        false,
        NormalizeHotkey(EventTypeHotkey),
        EventTypeIconKey),
    CancellationToken.None);
```

### 2) Фиксация start/end по hotkey

На каждое нажатие:

```csharp
var tagEvent = await _eventCaptureService.RegisterHotkeyPressAsync(
    projectId,
    pressedHotkey,
    currentFrame,
    selectedTeamSide,
    CancellationToken.None);
```

`tagEvent.IsOpen`:

- `true`: событие только что открыто
- `false`: событие закрыто вторым нажатием

### 3) Редактирование таймкодов

Редактирование выполняется через `UpsertTagEventAsync` с тем же `Id`:

```csharp
await _repository.UpsertTagEventAsync(
    existingTagEvent with { StartFrame = newStart, EndFrame = newEnd },
    CancellationToken.None);
```

### 4) Выборки для UI

Открытые события:

```csharp
await _repository.GetTagEventsAsync(projectId, new TagQuery(null, null, null, null, null, true), ct);
```

Закрытые события:

```csharp
await _repository.GetTagEventsAsync(projectId, new TagQuery(null, null, null, null, null, false), ct);
```

Фильтр по стороне команды:

```csharp
await _repository.GetTagEventsAsync(projectId, new TagQuery(null, null, null, null, TeamSide.Home, null), ct);
```

## Встроенные события для теста

Сейчас backend создает дефолтные хоккейные типы:

- Goal (`G`)
- Shot On Goal (`S`)
- Penalty (`P`)
- Save (`V`)
- Faceoff (`F`)

Файл:

- `backend/VideoAnalysis.Core/Models/HockeyTagPresets.cs`

## Definition of Done (frontend)

1. Пользователь может создать тип события с `name/hotkey/icon/color`.
2. При нажатии hotkey два раза событие корректно открывается и закрывается.
3. События сохраняются в frame.
4. Можно редактировать start/end и удалять событие.
5. Фильтрация по `TeamSide` и `IsOpen` работает в UI.
