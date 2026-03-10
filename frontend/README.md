# Frontend area

## Goal

Frontend is responsible only for desktop UI, user interaction flow, and presentation state.

Backend behavior must be consumed through contracts. Frontend must not own persistence, media processing, export pipelines, or cloud implementation details.

## Folder structure

- `VideoAnalysis.App/Bootstrap`
  - application startup
  - Avalonia app initialization
  - dependency injection wiring for the desktop app
- `VideoAnalysis.App/Views`
  - Avalonia windows and visual layout
  - code-behind only for view-specific UI integration
- `VideoAnalysis.App/ViewModels/Base`
  - common base types for view models
- `VideoAnalysis.App/ViewModels/Shell`
  - top-level screen view models
  - workflow/state orchestration for full screens
- `VideoAnalysis.App/ViewModels/Items`
  - small display models used by lists and repeated UI elements
- `VideoAnalysis.App/Configuration`
  - app-local settings and configuration persistence

## Rules for frontend developers

- Write XAML and layout in `Views`
- Write screen logic and commands in `ViewModels/Shell`
- Write reusable display item models in `ViewModels/Items`
- Write startup and DI wiring only in `Bootstrap`
- Write local settings code only in `Configuration`

## Do not put here

- SQLite logic
- FFmpeg commands
- S3/Yandex upload code
- domain/business rules that belong to backend
- direct infrastructure implementation details

## Source of truth

- `docs/TEAM_SPLIT.md`
- `docs/architecture/FRONTEND_STRUCTURE.md`
