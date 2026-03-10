# VideoAnalysis.App

Frontend Avalonia desktop application.

## Structure

- `Bootstrap`
  - app startup and DI wiring
- `Views`
  - Avalonia windows and code-behind
- `ViewModels/Base`
  - base view model types
- `ViewModels/Shell`
  - screen-level view models
- `ViewModels/Items`
  - list and item presentation models
- `Configuration`
  - local app settings

## Rule

Frontend code should call backend contracts and render state.

Do not add database, FFmpeg, storage, or export implementation details here.
