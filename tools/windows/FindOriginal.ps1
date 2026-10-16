# Read only the explicitly configured installation or wiz_data beside the runner checkout.
$ErrorActionPreference = 'Stop'
$explicit = @($env:WIZ8_INSTALLATION, $env:NEO_WIZ8_ROOT) | Where-Object { $_ } | Select-Object -First 1
if ($explicit) {
    $roots = @($explicit)
} else {
    # GitHub workspace = _work/repo/repo; runner workspace = _work/repo.
    # Support wiz_data in the checkout, beside it, or directly in _work.
    $workspaces = @($env:GITHUB_WORKSPACE, $env:RUNNER_WORKSPACE)
    if ($env:RUNNER_WORKSPACE) { $workspaces += Split-Path $env:RUNNER_WORKSPACE -Parent }
    $roots = @($workspaces | Where-Object { $_ } | Select-Object -Unique | ForEach-Object {
        Join-Path $_ 'wiz_data/Wizardry8'
    })
}
$archives = @($roots | ForEach-Object {
    $archive = Join-Path $_ 'Levels/LEVELS.SLF'
    if (Test-Path -LiteralPath $archive -PathType Leaf) { (Get-Item -LiteralPath $archive).FullName }
} | Select-Object -Unique)
if ($archives.Count -eq 0) {
    throw 'Missing wiz_data/Wizardry8/Levels/LEVELS.SLF in the runner work directory. Alternatively set repository variable WIZ8_ROOT to the Wizardry8 directory.'
}
if ($archives.Count -gt 1) { throw 'Multiple wiz_data copies found; select one using repository variable WIZ8_ROOT.' }
[IO.File]::AppendAllText($env:GITHUB_ENV, "NEO_LEVELS_ARCHIVE=$($archives[0])" + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host 'Found LEVELS.SLF in the configured original-data directory.'
