# Iteration 008 — CI environment reuse

The user requested faster iterations and chose the main Windows computer for a possible persistent runner. No runner has been installed or registered by the assistant. The user subsequently requested immediate self-hosted routing, committed as `c73ff4a6739149956580f9354464a4805250aa50`; Windows preview now selects `[self-hosted, Windows, X64]` without a hosted fallback. Registration/startup on the physical machine is covered by [runner setup](windows-runner.md).

## Changes

- Detect and pin an already-installed .NET 8 SDK. Install it only if missing.
- Cache NuGet on hosted Windows/Linux and the pinned Godot editors. Windows keeps only its export templates rather than retaining/downloading the full 1.27 GB cross-platform archive for every job.
- Self-hosted Windows keeps tools outside the checked-out project and skips Actions cache restore overhead.
- Disable Windows PowerShell's slow download progress rendering in the launcher. Real public release download, SHA256 verification and offline checks remain enabled.
- Allow re-running a completed source commit without overwriting its immutable published release.
- Route Windows builds directly to `[self-hosted, Windows, X64]`, as requested; there is no hosted fallback. Windows workflow is restricted to the original repository's `main`; PR verification remains hosted. These conditions are not a sandbox for arbitrary workflow files in this public repository.

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

This is approximately 59% less job execution time. Queue delay is excluded; hosted machines/network conditions vary. Do not infer a guaranteed duration from one run. All required tests/export/launcher checks passed; the cache fill succeeded. A subsequent rerun verified cache hits but did not complete; see the warm-cache observation below.

Code commit: `38e4d4f07afca18fb409f64faa72e7eeb065c6e1`. Gameplay mechanics did not change.

## Warm-cache observation

Attempt 2 restored both NuGet and Godot caches; Godot preparation took 1 second and .NET client build completed in 2.66 seconds. Godot then stalled during headless editor import after an EditorSettings diagnostic, so the attempt was canceled when a newer job superseded it. A complete faster warm-run result is **not** claimed. Build/import/export were separated, each with a timeout; obsolete previews now cancel instead of holding the queue.

## Self-hosted routing proof

Run https://github.com/Aetton/neowizardry/actions/runs/38651263873 started job 116150636202 on runner `AETTON-PC` (runner ID22; labels `self-hosted`, `Windows`, `X64`) at 2026-10-14T23:00:18Z. This confirms that the user's registered machine is accepting the Windows build. First local build outcome and performance are still being checked.

The first local attempt failed in actions/setup-python because script execution was disabled. The self-hosted path now uses BootstrapHost.ps1 from built-in PowerShell with process-only execution permission, provisioning portable tools in the runner account's app-data folder. Its bootstrap passed on AETTON-PC in 61 seconds (run38651895843); subsequent runs retain those tools. Publication explicitly sets GH_REPO, so the checkout archive fallback works even when Git is not installed.

The next local attempt passed tool bootstrap and Godot preparation but failed two Python symlink security tests with WinError 1314. The user then restarted the runner with administrative/service configuration; attempt 2 was requested to verify the actual runtime privileges before claiming a successful self-hosted build.

Attempt 2 (job 116154745198) ran on AETTON-PC as NetworkService. Bootstrap and Godot preparation succeeded, using a fresh cache under C:\Windows\ServiceProfiles\NetworkService; both symlink tests again failed with WinError 1314. Installing the service did not grant its runtime account symbolic-link creation rights. No self-hosted export or release success is claimed.

## Successful self-hosted verification

Attempt 3 passed on AETTON-PC after the service-account symlink permission was corrected: https://github.com/Aetton/neowizardry/actions/runs/38651895843/attempts/3 (job 116157802897). It ran as NetworkService from 2026-10-14T23:21:15Z to 23:23:33Z: **138 seconds / 2m18s**, excluding queue delay. Persistent tool bootstrap took 2 seconds; retained Godot preparation took 11 seconds. Gameplay/updater checks, client build, resource import, export, both exported-game smoke checks, publication and public launcher download/offline verification all passed. Windows preview 10 was published for source cefe8840dbd2c213af76f0bcb1d36a92e27f474d. This establishes a complete passing self-hosted build; the existing-release rerun publication branch remains unverified.
