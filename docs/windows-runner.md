# Persistent Windows build runner

The user chose the main Windows computer. At the user's subsequent explicit request, Windows preview now directly selects `[self-hosted, Windows, X64]`. A matching online runner must exist; otherwise the job queues. Linux/PR verification remains hosted.

## Automatic first-job bootstrap

Use Windows x64. The first self-hosted job runs `BootstrapHost.ps1` using built-in Windows PowerShell with process-scoped execution permission. It retains portable PowerShell 7, Python 3.12.10, .NET SDK 8 and GitHub CLI under `%LOCALAPPDATA%/NeoWizardryBuildTools` when required. It does not set machine/user execution policy or install a service. Subsequent jobs reuse these tools.

Python is the official embedded stdlib distribution. Its private sitecustomize adds the current checkout to sys.path for the project's stdlib-only tools/tests. No global Python/pip installation is required. Git is optional for the current project: checkout can use the GitHub archive fallback, and the publishing step explicitly sets GH_REPO.

Godot and Windows templates are retained by the next preparation step. First provisioning can take substantially longer than a warm build, depending on download speed.

## Register and start

1. Open https://github.com/Aetton/neowizardry/settings/actions/runners/new and choose **Windows / x64**.
2. Follow GitHub's current download and checksum instructions. Use a folder outside any project checkout, for example `%LOCALAPPDATA%/NeoWizardryRunner`.
3. Use GitHub's generated config command, appending `--name kirill-win --labels neowizardry-windows --work _work`. Registration uses the short-lived token shown by GitHub; keep it on the machine rather than in repository files/chat.
4. For persistent operation, configure the runner as a Windows service from an elevated terminal and deliberately choose its service account. If already registered without a service, remove the registration using GitHub’s generated removal command, then configure it again and select service installation. Use the same Windows account to retain the existing app-data tool cache.
5. The Python security checks create file and directory symbolic links. The actual runner account/process must have permission to create them; elevating the configuration terminal alone does not grant this privilege to a service account. Check this before expecting the complete build to pass. For console operation, start `./run.cmd` in an elevated terminal and wait for **Listening for Jobs**.

The runner connects outward; no inbound port forwarding is needed. Keep the machine awake and the runner process running while building.

## Workflow routing

The workflow is already routed directly to the runner. Its default labels `self-hosted`, `Windows`, `X64` are sufficient; the optional `neowizardry-windows` label can still identify this project's machine. No repository variable is needed.

Start the registered runner and check that the queued **Windows preview** job chose it, used an installed .NET 8 SDK, and reports retained Godot tools on subsequent jobs.

There is no hosted fallback while the computer is offline: jobs wait for matching labels. To deliberately return to hosted Windows, edit the workflow's `runs-on` back to `windows-latest`.

## Persistent tool locations

- Godot editor and only Windows templates: `%LOCALAPPDATA%/NeoWizardryBuildTools/godot-4.5.1-v1`.
- NuGet: the runner account's `.nuget/packages`.
- Portable Python, PowerShell and GitHub CLI: `%LOCALAPPDATA%/NeoWizardryBuildTools`.
- Project checkout/builds: the runner `_work` folder, isolated from the user's manually downloaded game and its saves.

Hosted runners restore Godot/NuGet through Actions cache. Self-hosted jobs use their existing disk and skip those archive restores. SDK selection pins an installed 8.x SDK for the job rather than downloading a newer patch or accidentally selecting SDK 10. Gameplay checks/export/smoke remain enabled.

## Public repository execution

GitHub recommends private repositories for self-hosted runners because untrusted PR workflows can execute on the host. NeoWizardry is public. This workflow checks `main` and the original repository, and the separate PR verification stays hosted; those checks are **not** a sandbox or a restriction on every possible workflow file. In **Settings → Actions → General**, require approval for **all outside collaborators**, and review workflows before approving external runs. Jobs inherit the runner account's access to local files and credentials.

Official guidance: https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners
