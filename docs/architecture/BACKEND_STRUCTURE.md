# Backend Structure

## Goal

The backend should be rebuilt as a clean modular monolith for a desktop product.

Deployment remains one desktop application, but code organization must enforce clean boundaries.

## Target folders

```text
backend/
  src/
    VideoAnalysis.Contracts/
    VideoAnalysis.Domain/
    VideoAnalysis.Application/
    VideoAnalysis.Infrastructure/
    VideoAnalysis.Host/
  tests/
    VideoAnalysis.UnitTests/
    VideoAnalysis.IntegrationTests/
```

## Responsibilities

### `VideoAnalysis.Contracts`

Contains the language of communication between frontend and backend.

Allowed contents:

- commands
- queries
- result DTOs
- public application service interfaces
- error/result envelopes

Must not contain:

- business logic
- repository implementations
- SQL, FFmpeg, S3, file IO

### `VideoAnalysis.Domain`

Contains core business concepts and invariants.

Allowed contents:

- entities
- value objects
- enums
- domain services
- validation rules

Must not contain:

- UI types
- persistence logic
- infrastructure dependencies

### `VideoAnalysis.Application`

Contains feature use cases.

Allowed contents:

- application services
- handlers
- orchestrators
- repository and gateway interfaces needed by use cases

Examples:

- import media
- create tag
- list tags
- build clips
- start export

### `VideoAnalysis.Infrastructure`

Contains technical implementations.

Allowed contents:

- SQLite repositories
- FFmpeg services
- Yandex S3 services
- file system access
- media playback adapters

### `VideoAnalysis.Host`

Contains composition root.

Allowed contents:

- DI registration
- configuration loading
- application startup wiring

This is the only backend project that should know how concrete implementations are connected together.

## Frontend interaction

Frontend should talk only to contracts.

The intended call chain is:

`View -> ViewModel -> Contracts interface -> Application -> Domain -> Infrastructure`

Frontend should never directly instantiate or call infrastructure classes.

## Suggested first backend modules

- `Projects`
- `MediaImport`
- `Tags`
- `Annotations`
- `ClipCompilation`
- `Export`
- `Statistics` later

## Suggested first contract surface

- `IProjectAppService`
- `IMediaImportAppService`
- `ITagAppService`
- `IAnnotationAppService`
- `IClipAppService`
- `IExportAppService`

## Legacy note

Current legacy backend projects:

- `backend/VideoAnalysis.Core`
- `backend/VideoAnalysis.Infrastructure`

They exist only to keep the MVP running. New backend work should target `backend/src/*`.
