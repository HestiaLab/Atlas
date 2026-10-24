## Summary

- What changed:
- Why:

## SRS Coverage

- Relevant SRS requirements (`docs/zh/SRS.md`, English mirror at `docs/en/SRS.md`): REQ-F-xxx
- New SRS requirements (if any): REQ-F-xxx

## Validation

- [ ] `dotnet build Atlas.sln` — all projects
- [ ] `dotnet test Atlas.sln` — all xUnit tests pass
- [ ] `dotnet format Atlas.sln --verify-no-changes` — no style violations
- [ ] `npm ci && npm run typecheck && npm run test:js && npm run build:js` in `src/Atlas.Blazor`
- [ ] Not run; reason:

## Impact

- Public API impact (`Atlas.Core` / `Atlas.Blazor`):
- NuGet packaging or dependency impact:
- Documentation impact (`docs/`): Chinese is authoritative under `docs/zh/`;
  keep the English mirror under `docs/en/` in sync for the bilingual list, or
  mark the English mirror `Outdated` when a normative document changes without
  a synchronized mirror.
- TypeScript / JS interop impact:

## AI Assistance

- [ ] AI assistance was used and disclosed — docs read, validation run, human verification recorded.
- [ ] No meaningful AI assistance was used.
