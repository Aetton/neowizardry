param([Parameter(Mandatory=$true)][string]$WorldRoot,
      [Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference='Stop'
$reportData=Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
foreach ($level in $reportData.levels) {
    $name=[IO.Path]::GetFileNameWithoutExtension($level.resource).ToLowerInvariant()
    $path=Join-Path $WorldRoot "$name.glb"
    $out=Join-Path $env:RUNNER_TEMP "$name-smoke.log"
    $err=Join-Path $env:RUNNER_TEMP "$name-smoke-error.log"
    $arguments="--headless --path client -- --world `"$path`" --world-smoke --world-triangles $($level.triangles)"
    $process=Start-Process -FilePath $env:GODOT -ArgumentList $arguments -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
    $completed=$false
    try {
        $completed=$process.WaitForExit(60000)
        if (!$completed) { $process.Kill();$process.WaitForExit(5000) | Out-Null }
    } finally {
        Get-Content -LiteralPath $out -ErrorAction SilentlyContinue
        Get-Content -LiteralPath $err -ErrorAction SilentlyContinue
    }
    if (!$completed) { throw "World preview timed out: $name" }
    if ($process.ExitCode -ne 0) { throw "World preview failed: $name" }
    if (!(Select-String -LiteralPath $out -Pattern 'PASS: world preview')) { throw "Missing world preview success marker: $name" }
}
