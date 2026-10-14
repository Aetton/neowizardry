# Iteration 005 — Windows-first testing

User decision (2026-10-15, Moscow): Windows is the primary manual test client. Android builds are used for mobile changes and milestones. The same C# core and Godot scene remain shared.

## Delivered

- Windows x64 export preset and Windows CI, with the .NET runtime bundled.
- Immutable numbered preview releases, published only after gameplay checks, launcher regression tests and exported-game smoke succeed.
- A small portable launcher: extract once, double-click `RunNeoWizardry.cmd` for current and future builds. No editor, SDK or system installation required.
- SHA256 validation before installation, staging, separate build directories, serialized launching and current-version marker replacement. Failed updates use the previous installed build; first-install errors are shown.
- Offline launch after an initial successful download. Old game builds are retained; game saves live in separate Godot user data.
- Android APK workflow changed to manual dispatch.
- Corrected a filesystem assumption in an asset test: Windows generally treats `a` and `A` as the same file; case-sensitive filesystems still test duplicate-name rejection.

## Validation

Windows PowerShell 5.1 updater regression checks cover initial install, already-current behavior without a repeated package download, checksum failure, unchanged current marker, successful next-version install, retained old version, network fallback, explicit offline mode, staging cleanup and unavailable first install.

Windows CI runs 12 Python tests and 125 portable C# checks. It exports and runs the Windows game, checking combat/equipment/save continuation and touch/pause/autosave through the exported executable. It also requires the bundled `coreclr.dll` and verifies public launcher download and offline mode after publishing.

Interactive Windows rendering, actual GPU behavior and the user's first launcher run remain user-device checks. Headless Windows smoke does not prove those.

Instructions: [Windows preview](windows.md). The distributed catalog is synthetic; original game databases and assets are excluded.

## Build evidence

Code commit: `637f162179214e8057fb54a2c0afb1257e8f3e6f`. Windows CI succeeded: https://github.com/Aetton/neowizardry/actions/runs/38643249297

First completed preview: https://github.com/Aetton/neowizardry/releases/tag/windows-build-3

- Launcher ZIP: 2271 bytes.
- Game ZIP: 69209721 bytes (about 66 MiB).
- Public first download and explicit offline mode both passed on Windows, without a GitHub token in the launcher.
- Launcher unit checks: 13. Python tests: 12. Portable C# checks: 125. Both exported-executable smoke modes passed.

Download the launcher once; later launches check for newly published builds. Releases remain separate from transient Actions artifacts.
