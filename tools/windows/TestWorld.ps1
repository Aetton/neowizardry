param([Parameter(Mandatory=$true)][string]$WorldRoot,
      [Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference='Stop'
$reportData=Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
foreach ($level in $reportData.levels) {
    $name=[IO.Path]::GetFileNameWithoutExtension($level.resource).ToLowerInvariant()
    $path=Join-Path $WorldRoot "$name.glb"
    $values=@($level.preview.render_bounds[0]) + @($level.preview.render_bounds[1])
    $bounds=($values | ForEach-Object { ([double]$_).ToString('R',[Globalization.CultureInfo]::InvariantCulture) }) -join ','
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=$env:GODOT
    $start.Arguments="--headless --path client -- --world `"$path`" --world-smoke --world-triangles $($level.triangles) --world-bounds $bounds"
    $start.UseShellExecute=$false
    $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true
    $start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::new()
    $process.StartInfo=$start
    try {
        if (!$process.Start()) { throw "Could not start world preview: $name" }
        # Own the process handle, rather than Start-Process -PassThru's detached handle.
        $stdout=$process.StandardOutput.ReadToEndAsync()
        $stderr=$process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(60000)) {
            $process.Kill();$process.WaitForExit()
            throw "World preview timed out: $name"
        }
        $output=$stdout.GetAwaiter().GetResult()
        $errors=$stderr.GetAwaiter().GetResult()
        $exitCode=$process.ExitCode
        Write-Host $output
        if ($errors) { Write-Host $errors }
        if ($exitCode -ne 0) { throw "World preview failed: $name (exit $exitCode)" }
        if ($output -notmatch 'PASS: world preview') { throw "Missing world preview success marker: $name" }
        if ($errors -match '(?m)^ERROR:') { throw "World preview reported an engine error: $name" }
    } finally { $process.Dispose() }
}
