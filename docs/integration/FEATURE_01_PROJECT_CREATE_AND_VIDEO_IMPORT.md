# Feature 01: Создание проекта и импорт видео

## Цель

С фронта создать проект и сразу привязать к нему видеофайл:

- название проекта
- опциональное описание
- команда хозяев
- команда гостей
- исходный путь к видео

Видео должно сохраняться в папку проекта, а не оставаться в случайном месте.

## Backend контракт (v1)

Интерфейс:

- `IProjectSetupService.CreateProjectWithVideoAsync(...)`

Файл:

- `backend/VideoAnalysis.Core/Abstractions/IProjectSetupService.cs`

Request DTO:

- `CreateProjectRequestDto`

Файл:

- `backend/VideoAnalysis.Core/Dtos/CreateProjectRequestDto.cs`

Поля request:

- `ProjectName` (`required`)
- `SourceVideoPath` (`required`)
- `VideoTitle` (`optional`)
- `Description` (`optional`)
- `HomeTeamName` (`optional`)
- `AwayTeamName` (`optional`)
- `MoveVideoToProjectFolder` (`bool`, default `true`)

Response DTO:

- `CreateProjectResultDto`

Файл:

- `backend/VideoAnalysis.Core/Dtos/CreateProjectResultDto.cs`

Поля response:

- `ProjectId`
- `ProjectFolderPath`
- `StoredVideoPath`
- `VideoTitle`

## Что происходит в backend

Реализация:

- `backend/VideoAnalysis.Infrastructure/Services/ProjectSetupService.cs`

Алгоритм:

1. Проверяет входные данные и наличие исходного файла.
2. Создает папку проекта в `%APPDATA%/VideoAnalysis/projects`.
3. Копирует видео в папку проекта.
4. Если `MoveVideoToProjectFolder = true`, удаляет исходный файл.
5. Сохраняет `Project` и `ProjectVideo` в SQLite.
6. Возвращает `ProjectId` и путь к сохраненному видео.

## Что хранится в БД

Таблицы:

- `Project`
- `ProjectVideo`

Реализация схемы:

- `backend/VideoAnalysis.Infrastructure/Persistence/SqliteProjectRepository.cs`

## Интеграция с frontend

Регистрация DI уже есть:

- `frontend/VideoAnalysis.App/Bootstrap/App.axaml.cs`

Что сделать во ViewModel:

1. Заинжектить `IProjectSetupService`.
2. Собрать данные формы.
3. Вызвать `CreateProjectWithVideoAsync`.
4. Обновить UI state (`ProjectId`, `SourceVideoPath`, статус).

Минимальный пример:

```csharp
var result = await _projectSetupService.CreateProjectWithVideoAsync(
    new CreateProjectRequestDto(
        ProjectName,
        SourceVideoPath,
        VideoTitle,
        Description,
        HomeTeamName,
        AwayTeamName,
        MoveVideoToProjectFolder: true),
    CancellationToken.None);

_projectId = result.ProjectId;
SourceVideoPath = result.StoredVideoPath;
StatusMessage = $"Project created: {result.ProjectFolderPath}";
```

## Ошибки, которые должен обработать фронт

- файл не найден
- пустое название проекта
- пустой путь к видео
- ошибка доступа к файлу/папке
- ошибка записи в БД

Показывать человеку короткое сообщение + логировать technical details.

## Definition of Done (frontend)

1. Форма создания проекта вызывает `CreateProjectWithVideoAsync`.
2. После успеха в UI показывается путь к видео уже в папке проекта.
3. Повторный запуск приложения подхватывает сохраненный проект/видео из БД.
4. Ошибки отображаются без падения приложения.
