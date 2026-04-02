# Video Analysis (Hockey MVP)

Desktop MVP for hockey video analytics on `C# + Avalonia`.

## Team-oriented repository layout

- `frontend/VideoAnalysis.App`: current Avalonia desktop UI.
- `backend/src/VideoAnalysis.Contracts`: future frontend-backend contracts.
- `backend/src/VideoAnalysis.Domain`: future domain model and business rules.
- `backend/src/VideoAnalysis.Application`: future use cases and orchestration.
- `backend/src/VideoAnalysis.Infrastructure`: future storage/media/export adapters.
- `backend/src/VideoAnalysis.Host`: future composition root for backend wiring.
- `backend/tests/VideoAnalysis.UnitTests`: future backend unit tests.
- `backend/tests/VideoAnalysis.IntegrationTests`: future backend integration tests.
- `backend/VideoAnalysis.Core`, `backend/VideoAnalysis.Infrastructure`: legacy MVP backend kept temporarily as reference/runtime support.
- `docs/`: architecture and team split documentation.

## Implemented now

- Import one match video file.
- Playback with timeline and frame-step.
- Tag presets (default hockey presets + custom presets).
- Tag events by frame ranges.
- Filter tags by player/period/text.
- Build clip segments from tags with pre/post roll.
- Add basic annotations (arrow/circle/text metadata).
- Export clips locally via FFmpeg.
- Optional upload of exported file to Yandex Object Storage (S3 API).
- SQLite project storage.

## Run

1. Restore and build:

```bash
dotnet restore VideoAnalysis.slnx
dotnet build VideoAnalysis.slnx
```

2. Run app:

```bash
dotnet run --project frontend/VideoAnalysis.App/VideoAnalysis.App.csproj
```

3. Run tests:

```bash
dotnet test backend/tests/VideoAnalysis.Tests/VideoAnalysis.Tests.csproj
```

## Team workflows

- Frontend developer works in `frontend/VideoAnalysis.App`
- Backend developer works in `backend/src/*`
- Legacy backend is frozen unless the team explicitly decides to patch MVP behavior
- Team split and target architecture are described in:
  - `docs/TEAM_SPLIT.md`
  - `docs/architecture/BACKEND_STRUCTURE.md`

## Frontend integration guides

- `docs/integration/FEATURE_01_PROJECT_CREATE_AND_VIDEO_IMPORT.md`
- `docs/integration/FEATURE_02_EVENT_TYPES_AND_TIMECODES.md`
- `docs/integration/FEATURE_03_PLAYLISTS.md`
- `docs/integration/MACOS_BUILD_AND_PACKAGE.md`

## Runtime dependencies

- `ffmpeg` available in PATH, or set custom path in app settings.
- `libvlc` runtime available for LibVLCSharp playback (bundle for production).

## macOS packaging

For Apple Silicon (`osx-arm64`) there is a repo script that builds a ready-to-run `.app` bundle:

```bash
chmod +x scripts/macos/package-app.sh
./scripts/macos/package-app.sh
```

See:

- `docs/integration/MACOS_BUILD_AND_PACKAGE.md`

If you do not have a Mac locally, use the GitHub Actions workflow:

- `.github/workflows/macos-package.yml`

## Notes

- Root storage location: `Documents/Video Analytics`
- Projects root: `Documents/Video Analytics/Projects`
- App settings: `Documents/Video Analytics/settings.json`
- Each project is a self-contained folder:
  - `project.db`
  - `project.json`
  - `media/`
  - `exports/`
- Imported video is copied into `media/` and the original file is preserved.
