<#
  End-to-end test for tools\update.ps1.

  Builds a fake "old install" (a 1.0.0 stub exe plus config/icons) and a fake
  release (the real exe from out\, packed as a zip) served through a file://
  latest.json, then checks:

    1. update 1.0.0 -> current: exit 0, exe/README/version.txt replaced
    2. launcher.json and Cache are byte-identical afterwards
    3. update.log is written, previous exe is backed up as .bak
    4. running again is a no-op ("already up to date")
    5. -Rollback restores the previous exe and version.txt

  Everything happens inside .smoke\update; nothing outside is touched.

  Usage:
    powershell -ExecutionPolicy Bypass -File tests\update-smoke.ps1
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$failures = New-Object System.Collections.ArrayList
function Assert([bool]$condition, [string]$message) {
    if ($condition) {
        Write-Host ("  ok   " + $message) -ForegroundColor DarkGray
    } else {
        Write-Host ("  FAIL " + $message) -ForegroundColor Red
        [void]$failures.Add($message)
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$work = Join-Path $RepoRoot '.smoke\update'
$install = Join-Path $work 'install'
$release = Join-Path $work 'release'
$tools = Join-Path $RepoRoot 'tools'
$exe = Join-Path $RepoRoot 'out\SmartLauncherNext.exe'
$updater = Join-Path $tools 'update.ps1'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

function Run-Updater([string[]]$extraArgs) {
    $cliArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $updater, '-InstallDir', $install, '-NoLaunch') + $extraArgs
    & powershell.exe @cliArgs | Out-Null
    return $LASTEXITCODE
}

try {
    if (-not (Test-Path $exe)) { throw "missing $exe; run build.ps1 first" }
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $install, $release | Out-Null

    $realVersion = (Get-Item $exe).VersionInfo.ProductVersion
    Write-Host ("target version: " + $realVersion)

    Write-Host '== fake old install (1.0.0) ==' -ForegroundColor Cyan
    $stubDir = Join-Path $work 'stub'
    New-Item -ItemType Directory -Force -Path $stubDir | Out-Null
    $stubCs = Join-Path $stubDir 'stub.cs'
    $stubCode = @'
using System.Reflection;
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
public class Stub { public static void Main() { } }
'@
    [System.IO.File]::WriteAllText($stubCs, $stubCode, $utf8)
    $stubExe = Join-Path $stubDir 'SmartLauncherNext.exe'
    & $csc /nologo /target:exe ("/out:" + $stubExe) $stubCs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'stub compile failed' }

    Copy-Item $stubExe (Join-Path $install 'SmartLauncherNext.exe')
    [System.IO.File]::WriteAllText((Join-Path $install 'version.txt'), '1.0.0', $utf8)
    [System.IO.File]::WriteAllText((Join-Path $install 'launcher.json'), '{"SchemaVersion":2,"HideWhenInactive":true,"Categories":[]}', $utf8)
    New-Item -ItemType Directory -Force -Path (Join-Path $install 'Cache\Icons') | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $install 'Cache\Icons\keep.bin'), [byte[]](1, 2, 3, 4, 5))
    $configHash = (Get-FileHash (Join-Path $install 'launcher.json')).Hash
    $cacheHash = (Get-FileHash (Join-Path $install 'Cache\Icons\keep.bin')).Hash

    Write-Host '== fake release ==' -ForegroundColor Cyan
    Copy-Item $exe (Join-Path $release 'SmartLauncherNext.exe')
    Copy-Item (Join-Path $RepoRoot 'README.md') (Join-Path $release 'README.md')
    [System.IO.File]::WriteAllText((Join-Path $release 'version.txt'), $realVersion, $utf8)
    Copy-Item (Join-Path $tools 'update.ps1') (Join-Path $release 'update.ps1')
    Copy-Item (Join-Path $tools 'update.cmd') (Join-Path $release 'update.cmd')

    $zip = Join-Path $work ("SmartLauncherNext-" + $realVersion + ".zip")
    [System.IO.Compression.ZipFile]::CreateFromDirectory($release, $zip)
    $latest = [ordered]@{
        version     = $realVersion
        url         = ([uri]$zip).AbsoluteUri
        sha256      = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        size        = (Get-Item $zip).Length
        publishedAt = (Get-Date).ToUniversalTime().ToString('o')
    }
    $latestJson = Join-Path $work 'latest.json'
    [System.IO.File]::WriteAllText($latestJson, ($latest | ConvertTo-Json), $utf8)
    $latestUri = ([uri]$latestJson).AbsoluteUri

    Write-Host '== update 1.0.0 -> current ==' -ForegroundColor Cyan
    $code = Run-Updater @('-LatestUrl', $latestUri)
    Assert ($code -eq 0) ("update exit code " + $code)
    Assert (((Get-Item (Join-Path $install 'SmartLauncherNext.exe')).VersionInfo.ProductVersion) -eq $realVersion) 'exe replaced with release version'
    Assert (((Get-Content (Join-Path $install 'version.txt') -Raw).Trim()) -eq $realVersion) 'version.txt updated'
    Assert (((Get-Content (Join-Path $install 'README.md') -Raw).Length) -gt 0) 'README replaced'
    Assert ((Get-FileHash (Join-Path $install 'launcher.json')).Hash -eq $configHash) 'launcher.json untouched'
    Assert ((Get-FileHash (Join-Path $install 'Cache\Icons\keep.bin')).Hash -eq $cacheHash) 'Cache untouched'
    Assert (Test-Path (Join-Path $install 'update.log')) 'update.log written'
    Assert (Test-Path (Join-Path $install 'SmartLauncherNext.exe.bak')) 'previous exe backed up'
    Assert (Test-Path (Join-Path $install 'update.ps1')) 'updater copied into install dir'

    Write-Host '== second run is a no-op ==' -ForegroundColor Cyan
    $logPath = Join-Path $install 'update.log'
    $linesBefore = @(Get-Content $logPath).Count
    $code2 = Run-Updater @('-LatestUrl', $latestUri)
    Assert ($code2 -eq 0) ("second run exit code " + $code2)
    $newLines = (@(Get-Content $logPath) | Select-Object -Skip $linesBefore) -join "`n"
    Assert ($newLines -match 'already up to date') 'second run reports already up to date'

    Write-Host '== rollback ==' -ForegroundColor Cyan
    $code3 = Run-Updater @('-Rollback')
    Assert ($code3 -eq 0) ("rollback exit code " + $code3)
    Assert (((Get-Item (Join-Path $install 'SmartLauncherNext.exe')).VersionInfo.ProductVersion) -eq '1.0.0') 'rollback restores old exe'
    Assert (((Get-Content (Join-Path $install 'version.txt') -Raw).Trim()) -eq '1.0.0') 'rollback restores version.txt'

    Write-Host '== version.txt repair ==' -ForegroundColor Cyan
    [System.IO.File]::WriteAllText((Join-Path $install 'version.txt'), '9.9.9', $utf8)
    $code4 = Run-Updater @('-LatestUrl', $latestUri, '-CheckOnly')
    Assert ($code4 -eq 0) ("repair run exit code " + $code4)
    $repaired = (Get-Content (Join-Path $install 'version.txt') -Raw).Trim()
    Assert ($repaired -eq '1.0.0') ("version.txt repaired from exe (" + $repaired + ")")
}
catch {
    Write-Host ("UPDATE SMOKE ERROR: " + $_.Exception.Message) -ForegroundColor Red
    [void]$failures.Add($_.Exception.Message)
}
finally {
    if (-not $KeepArtifacts) {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($failures.Count -gt 0) {
    Write-Host ("UPDATE SMOKE FAIL (" + $failures.Count + ")") -ForegroundColor Red
    foreach ($f in $failures) { Write-Host ("  - " + $f) -ForegroundColor Red }
    exit 1
}

Write-Host 'UPDATE SMOKE PASS' -ForegroundColor Green
exit 0
