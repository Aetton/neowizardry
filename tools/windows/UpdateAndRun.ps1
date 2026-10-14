param([switch]$Offline, [switch]$NoLaunch)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$Repository = 'Aetton/neowizardry'

function Get-PreviewRelease {
    $headers = @{ 'User-Agent' = 'NeoWizardry-Launcher'; 'Accept' = 'application/vnd.github+json' }
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases?per_page=50" -Headers $headers -TimeoutSec 20
    $release = $releases | Where-Object { !$_.draft -and $_.tag_name -match '^windows-build-[0-9]+$' } |
        Sort-Object published_at -Descending | Select-Object -First 1
    if (!$release) { throw 'No published Windows build is available yet.' }
    return $release
}

function Save-Asset($Url, $Path) {
    if ($Url -notlike "https://github.com/$Repository/releases/download/*") { throw 'Unexpected download location.' }
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $Path -TimeoutSec 180
}

function Get-CurrentGame($Root) {
    $marker = Join-Path $Root 'current.txt'
    if (!(Test-Path -LiteralPath $marker)) { return $null }
    $version = (Get-Content -LiteralPath $marker -Raw).Trim()
    if ($version -notmatch '^windows-build-[0-9]+$') { throw 'Invalid local version marker.' }
    $exe = Join-Path $Root "builds/$version/NeoWizardry.exe"
    if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'The installed build is incomplete.' }
    return $exe
}

function Update-NeoWizardry($Root, [switch]$SkipUpdate) {
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    $current = Get-CurrentGame $Root
    if ($SkipUpdate) {
        if (!$current) { throw 'Launch online once before using offline mode.' }
        return $current
    }
    $stage = Join-Path $Root ('staging-' + [Guid]::NewGuid().ToString('N'))
    try {
        $release = Get-PreviewRelease
        $manifestAsset = @($release.assets | Where-Object name -eq 'build.json')
        $packageAsset = @($release.assets | Where-Object name -eq 'neowizardry-windows-x64.zip')
        if ($manifestAsset.Count -ne 1 -or $packageAsset.Count -ne 1) { throw 'Release is missing its build manifest or package.' }
        New-Item -ItemType Directory -Path $stage | Out-Null
        $manifestPath = Join-Path $stage 'build.json'
        Save-Asset $manifestAsset[0].browser_download_url $manifestPath
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.schema -ne 1 -or $manifest.version -ne $release.tag_name -or
            $manifest.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or $manifest.commit -notmatch '^[a-f0-9]{40}$') {
            throw 'Unsupported build manifest.'
        }
        if ($current -and (Split-Path (Split-Path $current -Parent) -Leaf) -eq $manifest.version) {
            Write-Host "Already current: $($manifest.version)"
            return $current
        }
        Write-Host "Downloading $($manifest.version)..."
        $zip = Join-Path $stage 'game.zip'
        Save-Asset $packageAsset[0].browser_download_url $zip
        if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'Package SHA256 mismatch.' }
        $unpacked = Join-Path $stage 'game'
        Expand-Archive -LiteralPath $zip -DestinationPath $unpacked
        if (!(Test-Path -LiteralPath (Join-Path $unpacked 'NeoWizardry.exe') -PathType Leaf)) { throw 'Package is missing the game executable.' }
        $builds = Join-Path $Root 'builds'
        New-Item -ItemType Directory -Force -Path $builds | Out-Null
        $destination = Join-Path $builds $manifest.version
        if (Test-Path -LiteralPath $destination) { throw 'A conflicting build folder already exists.' }
        Move-Item -LiteralPath $unpacked -Destination $destination
        $marker = Join-Path $Root 'current.txt'
        $tempMarker = Join-Path $stage 'current.txt'
        [IO.File]::WriteAllText($tempMarker, $manifest.version)
        if (Test-Path -LiteralPath $marker) { [IO.File]::Replace($tempMarker, $marker, [NullString]::Value) }
        else { [IO.File]::Move($tempMarker, $marker) }
        Write-Host "Ready: $($manifest.version) ($($manifest.commit.Substring(0,7)))"
        return (Join-Path $destination 'NeoWizardry.exe')
    } catch {
        if (!$current) { throw }
        Write-Warning "Update failed: $($_.Exception.Message) Using the installed build."
        return $current
    } finally {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $lock = $null
    try {
        $root = Join-Path $env:LOCALAPPDATA 'NeoWizardry'
        New-Item -ItemType Directory -Force -Path $root | Out-Null
        $lock = [IO.File]::Open((Join-Path $root 'launcher.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
        $exe = Update-NeoWizardry $root -SkipUpdate:$Offline
        if (!$NoLaunch) {
            $game = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -Wait -PassThru
            if ($game.ExitCode -ne 0) { throw "Game exited with code $($game.ExitCode)." }
        }
    } catch {
        Write-Host "NeoWizardry: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    } finally {
        if ($lock) { $lock.Dispose() }
    }
}
