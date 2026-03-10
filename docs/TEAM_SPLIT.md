# Team Split

## Purpose

This repository is split so frontend and backend can work in parallel without sharing implementation details.

The product is still a desktop application, but the codebase should evolve as a modular monolith:

- `frontend` owns UI
- `backend` owns domain, use cases, storage, media processing
- both sides meet on explicit contracts

## Current state

There are two backend states in the repository:

- `backend/src/*`
  - target clean backend structure for new development
  - use this for all new backend planning and implementation
- `backend/VideoAnalysis.Core` and `backend/VideoAnalysis.Infrastructure`
  - legacy MVP backend from the first iteration
  - keep only as temporary reference
  - do not extend with new features unless the team explicitly decides otherwise

## Ownership

- Frontend developer:
  - `frontend/VideoAnalysis.App`
  - structure rules are defined in `docs/architecture/FRONTEND_STRUCTURE.md`
- Backend developer:
  - `backend/src/VideoAnalysis.Contracts`
  - `backend/src/VideoAnalysis.Domain`
  - `backend/src/VideoAnalysis.Application`
  - `backend/src/VideoAnalysis.Infrastructure`
  - `backend/src/VideoAnalysis.Host`
  - `backend/tests/VideoAnalysis.UnitTests`
  - `backend/tests/VideoAnalysis.IntegrationTests`

## Boundary between frontend and backend

Frontend must depend only on contracts exposed by backend.

Frontend is allowed to know:

- commands
- queries
- result DTOs
- view-friendly backend models that are explicitly exposed
- application service interfaces

Frontend must not know:

- SQLite details
- FFmpeg commands
- Yandex S3 implementation details
- file system layout
- repository implementation details

Backend must not know:

- Avalonia views
- Avalonia controls
- view model state
- UI-specific interaction logic

## Working rule

Every feature should move in this order:

1. Define or update contract in `Contracts`
2. Define domain rules in `Domain` if needed
3. Implement use case in `Application`
4. Implement technical adapters in `Infrastructure`
5. Wire everything in `Host`
6. Consume contract from `frontend`

## Practical example

Feature: "Create a tag"

1. Backend adds `CreateTagCommand` and `ITagAppService` contract
2. Backend adds validation/domain rules
3. Backend implements the use case
4. Backend persists it through infrastructure
5. Frontend calls `ITagAppService.CreateTagAsync(...)`
6. Frontend only renders result or error

## Build and run

- Full solution:
  - `dotnet build VideoAnalysis.slnx`
- Current desktop app:
  - `dotnet run --project frontend/VideoAnalysis.App/VideoAnalysis.App.csproj`
- Legacy backend tests:
  - `dotnet test backend/tests/VideoAnalysis.Tests/VideoAnalysis.Tests.csproj`

## Important note

Until the new backend is implemented, the app still runs on the legacy backend projects.

That is intentional. The new folder structure exists so the team can rebuild the backend cleanly without blocking frontend work.
