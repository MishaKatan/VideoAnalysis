# Video Analysis (Hockey MVP)

Desktop MVP for hockey video analytics on `C# + Avalonia`.

## Team-oriented repository layout

- `backend/VideoAnalysis.Core`: domain models, interfaces, use-case rules.
- `backend/VideoAnalysis.Infrastructure`: SQLite, FFmpeg, Yandex S3, LibVLC service implementations.
- `backend/tests/VideoAnalysis.Tests`: backend unit/integration tests.
- `frontend/VideoAnalysis.App`: Avalonia desktop UI (MVVM).
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

- Backend workspace: open `VideoAnalysis.Backend.slnf`
- Frontend workspace: open `VideoAnalysis.Frontend.slnf`
- Full workspace: open `VideoAnalysis.slnx`

## Runtime dependencies

- `ffmpeg` available in PATH, or set custom path in app settings.
- `libvlc` runtime available for LibVLCSharp playback (bundle for production).

## Notes

- App data location: `%APPDATA%/VideoAnalysis`
- SQLite DB: `%APPDATA%/VideoAnalysis/video-analysis.db`
- Settings JSON: `%APPDATA%/VideoAnalysis/settings.json`
