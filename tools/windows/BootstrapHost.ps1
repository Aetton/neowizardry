# Run with Windows PowerShell 5.1 -ExecutionPolicy Bypass; changes apply to this job only.
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$root=Join-Path $env:LOCALAPPDATA 'NeoWizardryBuildTools'
New-Item -ItemType Directory -Force -Path $root | Out-Null

function Write-ActionFile($File,$Line) {
    [IO.File]::AppendAllText($File,$Line+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
}
function Publish-Path($Path) { Write-ActionFile $env:GITHUB_PATH $Path }
function Download-Zip($Url,$Destination) {
    $zip=Join-Path $env:RUNNER_TEMP ([Guid]::NewGuid().ToString('N')+'.zip')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $zip -TimeoutSec 300
        New-Item -ItemType Directory -Force -Path $Destination | Out-Null
        Expand-Archive -LiteralPath $zip -DestinationPath $Destination -Force
    } finally { if(Test-Path $zip){Remove-Item $zip -Force} }
}
function Release-Asset($Repository,$Pattern) {
    $headers=@{'User-Agent'='NeoWizardry-build-bootstrap'}
    $release=Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers -TimeoutSec 30
    $asset=$release.assets | Where-Object { $_.name -match $Pattern } | Select-Object -First 1
    if(!$asset){throw "Missing portable release asset: $Repository"}
    return $asset.browser_download_url
}

if(!(Get-Command pwsh -ErrorAction SilentlyContinue)) {
    $pwsh=Join-Path $root 'powershell'
    if(!(Test-Path (Join-Path $pwsh 'pwsh.exe'))) {
        Download-Zip (Release-Asset 'PowerShell/PowerShell' '-win-x64.zip$') $pwsh
    }
    Publish-Path $pwsh
}

$python=Join-Path $root 'python-3.12.10'
if(!(Test-Path (Join-Path $python 'python.exe'))) {
    Download-Zip 'https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip' $python
    # Stdlib-only tooling needs the current checkout on sys.path; no pip/global install.
    Add-Content (Join-Path $python 'python312._pth') 'import site'
    Set-Content (Join-Path $python 'sitecustomize.py') 'import os, sys; sys.path.insert(0, os.getcwd())'
}
Publish-Path $python

$dotnet=Get-Command dotnet -ErrorAction SilentlyContinue
$hasSdk=$false
if($dotnet) { $hasSdk=@(& $dotnet.Source --list-sdks | Where-Object { $_ -match '^8\.' }).Count -gt 0 }
if(!$hasSdk) {
    $sdk=Join-Path $root 'dotnet'
    $exe=Join-Path $sdk 'dotnet.exe'
    $retained=Test-Path $exe
    if($retained){$retained=@(& $exe --list-sdks | Where-Object { $_ -match '^8\.' }).Count -gt 0}
    if(!$retained) {
        $installer=Join-Path $env:RUNNER_TEMP 'neo-dotnet-install.ps1'
        Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -TimeoutSec 60
        & $installer -Channel '8.0' -InstallDir $sdk -NoPath
        if($LASTEXITCODE){throw 'Local .NET SDK installation failed'}
    }
    Publish-Path $sdk
    Write-ActionFile $env:GITHUB_ENV "DOTNET_ROOT=$sdk"
}

if(!(Get-Command gh -ErrorAction SilentlyContinue)) {
    $cli=Join-Path $root 'github-cli'
    $binary=Get-ChildItem $cli -Recurse -Filter gh.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if(!$binary) {
        Download-Zip (Release-Asset 'cli/cli' '_windows_amd64.zip$') $cli
        $binary=Get-ChildItem $cli -Recurse -Filter gh.exe | Select-Object -First 1
    }
    if(!$binary){throw 'Portable GitHub CLI missing'}
    Publish-Path $binary.Directory.FullName
}
Write-Host "Persistent build tools ready under $root"
