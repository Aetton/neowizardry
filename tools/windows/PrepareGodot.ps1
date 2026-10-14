param([Parameter(Mandatory=$true)][string]$ToolsRoot)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$version='4.5.1'
$editor=Join-Path $ToolsRoot 'godot'
$templates=Join-Path $ToolsRoot 'templates'
$ready=Join-Path $ToolsRoot 'ready.txt'
if(!(Test-Path -LiteralPath $ready)) {
    New-Item -ItemType Directory -Force -Path $ToolsRoot | Out-Null
    $download=Join-Path $env:RUNNER_TEMP ('neo-tools-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $download | Out-Null
    try {
        $editorZip=Join-Path $download 'godot.zip'
        $templateZip=Join-Path $download 'templates.tpz'
        curl.exe -fL --retry 3 "https://github.com/godotengine/godot/releases/download/$version-stable/Godot_v$version-stable_mono_win64.zip" -o $editorZip
        if($LASTEXITCODE){throw 'Godot editor download failed'}
        curl.exe -fL --retry 3 "https://github.com/godotengine/godot/releases/download/$version-stable/Godot_v$version-stable_mono_export_templates.tpz" -o $templateZip
        if($LASTEXITCODE){throw 'Godot template download failed'}
        if(Test-Path $editor){Remove-Item $editor -Recurse -Force}
        if(Test-Path $templates){Remove-Item $templates -Recurse -Force}
        Expand-Archive -LiteralPath $editorZip -DestinationPath $editor
        New-Item -ItemType Directory -Path $templates | Out-Null
        # Keep only the Windows templates; discard the 1.27 GB cross-platform archive.
        python -c "import zipfile,pathlib,sys; z=zipfile.ZipFile(sys.argv[1]); root=pathlib.Path(sys.argv[2]); [(root/pathlib.PurePosixPath(n).name).write_bytes(z.read(n)) for n in z.namelist() if n.startswith('templates/windows') and not n.endswith('/')]" $templateZip $templates
        if($LASTEXITCODE){throw 'Godot template extraction failed'}
        Set-Content -LiteralPath $ready -Value $version
    } finally { Remove-Item -LiteralPath $download -Recurse -Force }
} else { Write-Host 'Using retained Godot editor and Windows templates.' }
$godot=Get-ChildItem $editor -Recurse -Filter '*_console.exe' | Select-Object -First 1
if(!$godot -or !(Test-Path (Join-Path $templates 'windows_debug_x86_64.exe'))) { throw 'Godot tools cache is incomplete; remove this tools folder and retry.' }
$destination=Join-Path $env:APPDATA "Godot/export_templates/$version.stable.mono"
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item "$templates/*" $destination -Force
"GODOT=$($godot.FullName)" >> $env:GITHUB_ENV
