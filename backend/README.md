# Backend area

Projects:

- `VideoAnalysis.Core`: contracts, domain models, domain services.
- `VideoAnalysis.Infrastructure`: implementations for DB/media/export.
- `tests/VideoAnalysis.Tests`: backend tests.

Typical flow:

1. Add or update abstraction in `Core`.
2. Implement in `Infrastructure`.
3. Cover with tests.
