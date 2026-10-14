$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/UpdateAndRun.ps1"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('neo-updater-test-' + [Guid]::NewGuid().ToString('N'))
$checks = 0
function Check($Condition, $Name) {
    if (!$Condition) { throw $Name }
    $script:checks++
}
try {
    New-Item -ItemType Directory -Path $testRoot | Out-Null
    $fixture = Join-Path $testRoot 'fixture'
    New-Item -ItemType Directory -Path $fixture | Out-Null
    Set-Content -LiteralPath (Join-Path $fixture 'NeoWizardry.exe') -Value 'test executable placeholder'
    $fixtureZip = Join-Path $testRoot 'fixture.zip'
    Compress-Archive -Path "$fixture/*" -DestinationPath $fixtureZip
    $fixtureManifest = Join-Path $testRoot 'manifest.json'
    $script:releaseVersion = 'windows-build-1'
    $script:failNetwork = $false
    $script:badHash = $false
    $script:downloads = 0
    function Get-PreviewRelease {
        if ($script:failNetwork) { throw 'Simulated network failure' }
        return [PSCustomObject]@{ tag_name = $script:releaseVersion; assets = @(
            [PSCustomObject]@{name='build.json';browser_download_url='manifest'},
            [PSCustomObject]@{name='neowizardry-windows-x64.zip';browser_download_url='zip'}) }
    }
    function Save-Asset($Url, $Path) {
        $script:downloads++
        if ($Url -eq 'manifest') {
            $hash = (Get-FileHash $fixtureZip).Hash
            if ($script:badHash) { $hash = '0' * 64 }
            @{schema=1;version=$script:releaseVersion;sha256=$hash;commit=('a' * 40)} |
                ConvertTo-Json | Set-Content -LiteralPath $Path
        } else { Copy-Item -LiteralPath $fixtureZip -Destination $Path }
    }
    $install = Join-Path $testRoot 'installed'
    $exe = Update-NeoWizardry $install
    Check (Test-Path -LiteralPath $exe) 'First install failed'
    Check ((Get-Content (Join-Path $install 'current.txt')) -eq 'windows-build-1') 'First build marker'
    $before = $script:downloads
    Check ((Update-NeoWizardry $install) -eq $exe) 'Current build changed unexpectedly'
    Check ($script:downloads -eq $before + 1) 'Current build downloaded the package again'
    $script:releaseVersion = 'windows-build-2'; $script:badHash = $true
    Check ((Update-NeoWizardry $install) -eq $exe) 'Corrupt update did not fall back'
    Check (!(Test-Path (Join-Path $install 'builds/windows-build-2'))) 'Corrupt update was installed'
    Check ((Get-Content (Join-Path $install 'current.txt')) -eq 'windows-build-1') 'Corrupt update changed marker'
    $script:badHash = $false
    $updated = Update-NeoWizardry $install
    Check ($updated -ne $exe -and (Test-Path $updated)) 'New version not installed'
    Check (Test-Path $exe) 'Previous version deleted'
    $script:failNetwork = $true
    Check ((Update-NeoWizardry $install) -eq $updated) 'Offline fallback failed'
    Check ((Update-NeoWizardry $install -SkipUpdate) -eq $updated) 'Explicit offline launch failed'
    Check (@(Get-ChildItem $install -Directory -Filter 'staging-*').Count -eq 0) 'Staging files left behind'
    $failed = $false
    try { Update-NeoWizardry (Join-Path $testRoot 'empty') | Out-Null } catch { $failed = $true }
    Check $failed 'First install silently succeeded without network'
    Write-Host "PASS: $checks Windows updater checks"
} finally { Remove-Item -LiteralPath $testRoot -Recurse -Force }
