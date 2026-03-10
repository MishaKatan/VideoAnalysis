# Backend area

This folder currently contains two things:

- `src/*`
  - new clean backend structure for all future development
- `VideoAnalysis.Core` and `VideoAnalysis.Infrastructure`
  - legacy MVP backend kept temporarily so the current desktop app can still run

## New backend target

Use these folders for all new backend work:

- `src/VideoAnalysis.Contracts`
- `src/VideoAnalysis.Domain`
- `src/VideoAnalysis.Application`
- `src/VideoAnalysis.Infrastructure`
- `src/VideoAnalysis.Host`
- `tests/VideoAnalysis.UnitTests`
- `tests/VideoAnalysis.IntegrationTests`

## Rule

Do not add new product features into legacy backend projects unless it is a short-lived hotfix for the current MVP.

Use `docs/TEAM_SPLIT.md` and `docs/architecture/BACKEND_STRUCTURE.md` as the source of truth.
