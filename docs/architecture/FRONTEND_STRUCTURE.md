# Frontend Structure

## Goal

The frontend should stay predictable for any new Avalonia developer joining the project.

The guiding rule is:

`View -> ViewModel -> backend contract`

Frontend renders and orchestrates UI. Backend performs business and technical work.

## Target frontend layout

```text
frontend/
  VideoAnalysis.App/
    Bootstrap/
    Views/
    ViewModels/
      Base/
      Shell/
      Items/
    Configuration/
```

## Responsibilities

### `Bootstrap`

Contains application startup and app composition concerns.

Put here:

- `Program.cs`
- `App.axaml`
- `App.axaml.cs`
- service registration
- startup configuration

Do not put here:

- screen logic
- business workflows
- storage logic

### `Views`

Contains Avalonia windows and visual markup.

Put here:

- `*.axaml`
- `*.axaml.cs`
- visual tree
- control arrangement
- bindings

Allowed in code-behind:

- view-specific technical integration
- host control setup
- native UI bridge code that does not belong in view model

Do not put here:

- data loading rules
- storage logic
- export logic

### `ViewModels/Base`

Contains shared base classes and common view model helpers.

### `ViewModels/Shell`

Contains screen-level view models.

Put here:

- commands triggered by the user
- UI state for a whole screen
- coordination between multiple UI panels
- calls into backend contracts

Do not put here:

- direct infrastructure implementation
- database queries
- FFmpeg command construction

### `ViewModels/Items`

Contains small UI-facing models for repeated list items.

Use this for:

- list row display models
- small presentation-only item models

### `Configuration`

Contains frontend-local application settings.

Use this for:

- desktop app settings
- local config storage
- config loading/saving helpers

Do not use this for:

- backend state
- project storage
- business data persistence

## Practical writing rules

When a frontend developer needs to add something:

- new window or panel:
  - `Views`
- new screen behavior:
  - `ViewModels/Shell`
- new row/item representation:
  - `ViewModels/Items`
- new startup registration:
  - `Bootstrap`
- new local settings field:
  - `Configuration`

## Current mapping

- App startup:
  - `Bootstrap/App.axaml`
  - `Bootstrap/App.axaml.cs`
  - `Bootstrap/Program.cs`
- Main desktop window:
  - `Views/MainWindow.axaml`
  - `Views/MainWindow.axaml.cs`
- Main screen logic:
  - `ViewModels/Shell/MainWindowViewModel.cs`
- List item display models:
  - `ViewModels/Items/TagEventItemViewModel.cs`
  - `ViewModels/Items/AnnotationItemViewModel.cs`
- Shared base:
  - `ViewModels/Base/ViewModelBase.cs`
- App settings:
  - `Configuration/AppSettings.cs`
  - `Configuration/AppSettingsStore.cs`
