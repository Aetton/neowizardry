# Iteration 008 — CI environment reuse

The user requested faster iterations and chose the main Windows computer for a possible persistent runner. No runner has been installed or registered by the assistant. The user subsequently requested immediate self-hosted routing, committed as `c73ff4a6739149956580f9354464a4805250aa50`; Windows preview now selects `[self-hosted, Windows, X64]` without a hosted fallback. Registration/startup on the physical machine is covered by [runner setup](windows-runner.md).

## Changes

- Detect and pin an already-installed .NET 8 SDK. Install it only if missing.
- Cache NuGet on hosted Windows/Linux and the pinned Godot editors. Windows keeps only its export templates rather than retaining/downloading the full 1.27 GB cross-platform archive for every job.
- Self-hosted Windows keeps tools outside the checked-out project and skips Actions cache restore overhead.
- Disable Windows PowerShell's slow download progress rendering in the launcher. Real public release download, SHA256 verification and offline checks remain enabled.
- Allow re-running a completed source commit without overwriting its immutable published release.
- Select Windows runner labels through a repository variable; default is hosted. Windows workflow is restricted to the original repository's `main`; PR verification remains hosted. These conditions are not a sandbox for arbitrary workflow files in this public repository.

## Measured evidence

Baseline: Windows preview 6, job 116140821302, https://github.com/Aetton/neowizardry/actions/runs/38648225599

Optimized first run: job 116146035630, https://github.com/Aetton/neowizardry/actions/runs/38649967189/attempts/1

| Stage | Baseline | Optimized first run |
|---|---:|---:|
| Entire Windows job | 272 s | 111 s |
| SDK installation | 42 s | 0 s (skipped) |
| Godot preparation | 17 s | 19 s (first cache fill) |
| Gameplay/updater checks | 23 s | 23 s |
| Export and exported-game smoke | 36 s | 27 s |
| Public launcher download/offline | 123 s | 5 s |

This is approximately 59% less job execution time. Queue delay is excluded; hosted machines/network conditions vary. Do not infer a guaranteed duration from one run. All required tests/export/launcher checks passed; the cache fill succeeded. The same job is being re-run to verify cache hits and publication reuse.

Code commit: `38e4d4f07afca18fb409f64faa72e7eeb065c6e1`. Gameplay mechanics did not change.

## Warm-cache observation

Attempt 2 restored both NuGet and Godot caches; Godot preparation took 1 second and .NET client build completed in 2.66 seconds. Godot then stalled during headless editor import after an EditorSettings diagnostic, so the attempt was canceled when a newer job superseded it. A complete faster warm-run result is **not** claimed. Build/import/export were separated, each with a timeout; obsolete previews now cancel instead of holding the queue.
