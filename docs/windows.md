# Windows preview

Windows x64 is the primary manual testing target. Android remains a separate touch/lifecycle/performance gate. Both clients share the same C# rules and Godot scene.

## Start once, then update by launching

1. Open a published `Windows preview` at https://github.com/Aetton/neowizardry/releases.
2. Download `neowizardry-windows-launcher.zip` and extract both files into a normal user-writable folder.
3. Double-click `RunNeoWizardry.cmd`. Windows PowerShell checks published previews, downloads the game on first use and launches it.
4. For subsequent versions, use that same file again. No reinstall, Godot editor or .NET SDK is needed. The exported game includes its runtime.

The launcher accepts `-Offline` to skip the network check after a successful install. From PowerShell: `./RunNeoWizardry.cmd -Offline`. Keep the entire launcher folder together.

For direct portable use, download and extract **all** of `neowizardry-windows-x64.zip`, then run `NeoWizardry.exe`. Its adjacent PCK and managed/runtime folders are required. This route has no automatic update check.

## Update behavior

- Publishes immutable numbered prereleases after Windows gameplay tests, updater tests, export and exported-executable smoke pass. Draft releases are ignored.
- Uses public GitHub release downloads; no GitHub account or token is needed for normal launching.
- Checks SHA256 against `build.json`, unpacks into a staging directory, and changes the current version only after the candidate executable is present.
- Keeps earlier builds. Network/checksum/download failures fall back to the current installed build. On a first install, failures are shown and no game is launched.
- Serializes launcher use with a file lock. Close the existing launcher/game before launching another copy.
- Game files are under `%LOCALAPPDATA%/NeoWizardry/builds/<version>`. Launcher configuration is not a campaign save.

Game saves use Godot `user://`, resolved on Windows beneath `%APPDATA%/Godot/app_userdata/NeoWizardry — first incision/`. Updating builds does not remove this directory. The current prototype's save filenames are `prototype-save.json` and `prototype-autosave.json`.

The launcher runs in a console window and waits for the game to exit. It is an early development launcher, not a signed production installer. SHA256 checks download integrity against the repository's manifest; it is not independent publisher authentication. Save-schema compatibility remains a gameplay responsibility, not a guarantee of the updater.

## Windows CI

`.github/workflows/windows.yml` runs on a Windows runner with Godot 4.5.1 .NET, exports x64, then runs the exported executable's combat/equipment/save and touch/autosave checks. The launcher is tested using **Windows PowerShell 5.1**, matching its double-click entry point. CI does not verify the user's GPU or interactive Windows rendering.

The public builds package the synthetic staff catalog; original SLF/DBS resources stay outside releases. Android APK installation is needed when testing mobile-specific changes or milestone builds rather than every gameplay iteration.

Windows previews are built automatically on relevant `main` changes. Android export is now manual (`workflow_dispatch`) for mobile changes and milestone checks.

## CI speed and an optional persistent runner

Windows builds reuse an installed .NET 8 SDK, cache NuGet and the Godot editor/selected Windows templates, and suppress Windows PowerShell's expensive download progress rendering. The anonymous launcher still downloads and verifies the real published package; the check was not removed. Re-running a completed source commit keeps its already-published immutable release rather than replacing assets.

A local Windows runner can keep its tools on disk. At the user's explicit request, Windows preview now directly selects a self-hosted Windows x64 runner and queues if none is online. See [persistent runner setup](windows-runner.md).
