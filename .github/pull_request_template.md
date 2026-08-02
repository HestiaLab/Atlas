## Summary

- What changed:
- Why:

## SRS Coverage

- Relevant SRS requirements (`docs/en/SRS.md`): REQ-F-xxx
- New SRS requirements (if any): REQ-F-xxx

## Validation

- [ ] `dotnet build Atlas.sln` — all projects
- [ ] `dotnet test Atlas.sln` — all xUnit tests pass
- [ ] `dotnet format Atlas.sln --verify-no-changes` — no style violations
- [ ] `npm ci && npm run typecheck && npm run test:js && npm run build:js` in `src/Atlas.Blazor`
- [ ] Manual smoke: `dotnet run --project src/Atlas.Demo` and inspect layout
- [ ] Not run; reason:

## Impact

- Public API impact (`Atlas.Core` / `Atlas.Blazor`):
- NuGet packaging or dependency impact:
- Documentation impact (`docs/`): keep English canonical under `docs/en/`
  and mark the Chinese translation `Outdated` when a normative document
  changes without a synchronized translation.
- TypeScript / JS interop impact:

## AI Assistance

- [ ] AI assistance was used and disclosed — docs read, validation run, human verification recorded.
- [ ] No meaningful AI assistance was used.
