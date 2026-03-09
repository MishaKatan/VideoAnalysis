# Backend / Frontend split

## Goal

Provide clean ownership boundaries so backend and frontend can work in parallel with minimal merge conflicts.

## Ownership

- Backend owner:
  - `backend/VideoAnalysis.Core`
  - `backend/VideoAnalysis.Infrastructure`
  - `backend/tests/VideoAnalysis.Tests`
- Frontend owner:
  - `frontend/VideoAnalysis.App`

## Contract boundary

- Frontend can consume only abstractions and models from `VideoAnalysis.Core`.
- Frontend must not add persistence/ffmpeg/storage logic in UI layer.
- Backend must not add Avalonia/UI dependencies into backend projects.
- Shared behavior changes (DTO/interface signature changes in `Core`) require joint sync.

## Integration rules

- New backend capability:
  1. Define/update interface in `backend/VideoAnalysis.Core/Abstractions`.
  2. Add implementation in `backend/VideoAnalysis.Infrastructure`.
  3. Add tests in `backend/tests/VideoAnalysis.Tests`.
  4. Frontend integrates through existing abstractions in view models.
- Frontend feature changes:
  1. Use existing `Core` contracts.
  2. If contract is missing, open a backend task for interface extension.

## Build commands

- Full: `dotnet build VideoAnalysis.slnx`
- Backend tests: `dotnet test backend/tests/VideoAnalysis.Tests/VideoAnalysis.Tests.csproj`
- Frontend run: `dotnet run --project frontend/VideoAnalysis.App/VideoAnalysis.App.csproj`
