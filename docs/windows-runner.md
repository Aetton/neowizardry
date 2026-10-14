# Persistent Windows build runner

The user chose the main Windows computer. At the user's subsequent explicit request, Windows preview now directly selects `[self-hosted, Windows, X64]`. A matching online runner must exist; otherwise the job queues. Linux/PR verification remains hosted.

## One-time prerequisites

Use Windows x64. Install Git, PowerShell 7, .NET SDK 8 and GitHub CLI if they are missing. From a terminal with installation privileges:

```powershell
winget install --exact --id Git.Git
winget install --exact --id Microsoft.PowerShell
winget install --exact --id Microsoft.DotNet.SDK.8
winget install --exact --id GitHub.cli
```

Restart the terminal after installation. Check `git --version`, `pwsh --version`, `dotnet --list-sdks` (must include 8.x) and `gh --version`. Python 3.12 is provisioned by setup-python and retained in the runner's tool cache. Godot/editor templates and NuGet are provisioned on first use, then retained.

## Register and start

1. Open https://github.com/Aetton/neowizardry/settings/actions/runners/new and choose **Windows / x64**.
2. Follow GitHub's current download and checksum instructions. Use a folder outside any project checkout, for example `%LOCALAPPDATA%/NeoWizardryRunner`.
3. Use GitHub's generated config command, appending `--name kirill-win --labels neowizardry-windows --work _work`. Registration uses the short-lived token shown by GitHub; keep it on the machine rather than in repository files/chat.
4. Initially answer **No** to installing as a service. Start `./run.cmd` from a normal, non-admin terminal and wait for **Listening for Jobs**. A separate ordinary Windows user is preferable on a daily-use computer. Service installation is optional later and requires deliberate account selection.

The runner connects outward; no inbound port forwarding is needed. Keep the machine awake and the runner process running while building.

## Workflow routing

The workflow is already routed directly to the runner. Its default labels `self-hosted`, `Windows`, `X64` are sufficient; the optional `neowizardry-windows` label can still identify this project's machine. No repository variable is needed.

Start the registered runner and check that the queued **Windows preview** job chose it, used an installed .NET 8 SDK, and reports retained Godot tools on subsequent jobs.

There is no hosted fallback while the computer is offline: jobs wait for matching labels. To deliberately return to hosted Windows, edit the workflow's `runs-on` back to `windows-latest`.

## Persistent tool locations

- Godot editor and only Windows templates: `%LOCALAPPDATA%/NeoWizardryBuildTools/godot-4.5.1-v1`.
- NuGet: the runner account's `.nuget/packages`.
- Python: the runner's tool cache.
- Project checkout/builds: the runner `_work` folder, isolated from the user's manually downloaded game and its saves.

Hosted runners restore Godot/NuGet through Actions cache. Self-hosted jobs use their existing disk and skip those archive restores. SDK selection pins an installed 8.x SDK for the job rather than downloading a newer patch or accidentally selecting SDK 10. Gameplay checks/export/smoke remain enabled.

## Public repository execution

GitHub recommends private repositories for self-hosted runners because untrusted PR workflows can execute on the host. NeoWizardry is public. This workflow checks `main` and the original repository, and the separate PR verification stays hosted; those checks are **not** a sandbox or a restriction on every possible workflow file. In **Settings → Actions → General**, require approval for **all outside collaborators**, and review workflows before approving external runs. Jobs inherit the runner account's access to local files and credentials.

Official guidance: https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners
