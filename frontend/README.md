# Frontend area

Project:

- `VideoAnalysis.App`: Avalonia desktop UI (views + view models).

Rules:

- UI consumes backend behavior through `VideoAnalysis.Core` contracts.
- No direct persistence/ffmpeg/cloud implementation in frontend layer.
