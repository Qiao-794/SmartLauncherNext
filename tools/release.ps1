<#
  One-command release for SmartLauncher Next.

  Steps: bump version -> changelog -> build -> tests -> package -> publish -> deploy -> verify

  Usage:
    powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Version 1.3.6 -Notes "fix a","fix b"
    powershell -ExecutionPolicy Bypass -File tools\release.ps1 -SkipPublish -SkipDeploy
    powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Version 1.3.6 -Push

  Publishing needs GITHUB_TOKEN (or GH_TOKEN). Without it the publish step is
  skipped with a warning, so the script still produces build + package + deploy.
#>
param(
    [string]$Version = '',
    [string[]]$Notes = @(),
    [string]$InstallDir = 'D:\Tools\SmartLauncher_Next_Lite',
    [switch]$SkipTests,
    [switch]$SkipPublish,
    [switch]$SkipDeploy,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Step([string]$text) { Write-Host ("== " + $text + " ==") -ForegroundColor Cyan }
function Info([string]$text) { Write-Host ("   " + $text) -ForegroundColor DarkGray }
function Warn([string]$text) { Write-Host ("   WARN " + $text) -ForegroundColor Yellow }

function Assert-Exit([string]$what) {
    if ($LASTEXITCODE -ne 0) { throw "$what failed (exit $LASTEXITCODE)" }
}

$viPath = Join-Path $repo 'src\VersionInfo.cs'
$vi = [System.IO.File]::ReadAllText($viPath)
$m = [regex]::Match($vi, 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)')
if (-not $m.Success) { throw "cannot parse version from $viPath" }
$current = [version]($m.Groups[1].Value + '.' + $m.Groups[2].Value + '.' + $m.Groups[3].Value)

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (New-Object version($current.Major, $current.Minor, ($current.Build + 1))).ToString()
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.3.6, got: $Version" }

Step 'version'
Info ("$current -> $Version")
$vi = [regex]::Replace($vi, 'AssemblyVersion\("\d+\.\d+\.\d+\.\d+"\)', ('AssemblyVersion("' + $Version + '.0")'))
$vi = [regex]::Replace($vi, 'AssemblyFileVersion\("\d+\.\d+\.\d+\.\d+"\)', ('AssemblyFileVersion("' + $Version + '.0")'))
$vi = [regex]::Replace($vi, 'AssemblyInformationalVersion\("\d+\.\d+\.\d+"\)', ('AssemblyInformationalVersion("' + $Version + '")'))
[System.IO.File]::WriteAllText($viPath, $vi, $utf8)

Step 'changelog'
$clPath = Join-Path $repo 'CHANGELOG.md'
$cl = [System.IO.File]::ReadAllText($clPath)
if ($cl -notmatch ('(?m)^## ' + [regex]::Escape($Version) + '\b')) {
    if ($Notes.Count -eq 0) { $Notes = @('release ' + $Version) }
    $entry = '## ' + $Version + ' - ' + (Get-Date -Format 'yyyy-MM-dd') + "`r`n`r`n"
    foreach ($n in $Notes) { $entry += '- ' + $n + "`r`n" }
    $entry += "`r`n"
    $cl = [regex]::Replace($cl, '\A# Changelog\r?\n\r?\n', "# Changelog`r`n`r`n" + $entry)
    [System.IO.File]::WriteAllText($clPath, $cl, $utf8)
    Info ("added changelog entry with " + $Notes.Count + ' note(s)')
} else {
    Info 'changelog entry already exists'
}

Step 'build'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'build.ps1')
Assert-Exit 'build'
$exe = Join-Path $repo 'out\SmartLauncherNext.exe'
$builtVersion = (Get-Item $exe).VersionInfo.ProductVersion
Info ("exe " + (Get-Item $exe).Length + " bytes, version " + $builtVersion)
if ($builtVersion -ne $Version) { throw "exe version $builtVersion does not match $Version" }

if (-not $SkipTests) {
    Step 'smoke'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tests\smoke.ps1')
    Assert-Exit 'smoke'

    Step 'update smoke'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tests\update-smoke.ps1')
    Assert-Exit 'update smoke'
} else {
    Warn 'tests skipped'
}

Step 'package'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'package.ps1') -SkipBuild -Version $Version
Assert-Exit 'package'

if (-not $SkipPublish) {
    Step 'publish'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tools\publish.ps1') -Version $Version -DryRun
    Assert-Exit 'publish (dry run)'
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN) -and [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        Warn 'no GITHUB_TOKEN/GH_TOKEN, skipped the real publish'
    } else {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tools\publish.ps1') -Version $Version
        Assert-Exit 'publish'
    }
} else {
    Warn 'publish skipped'
}

if (-not $SkipDeploy) {
    Step 'deploy'
    $targetExe = Join-Path $InstallDir 'SmartLauncherNext.exe'
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue | ForEach-Object {
        $p = $_
        $path = $null
        try { $path = $p.Path } catch { }
        if ($path -and ($path -ieq $targetExe)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    }
    Start-Sleep -Milliseconds 400
    Copy-Item $exe $targetExe -Force
    Copy-Item (Join-Path $repo 'README.md') (Join-Path $InstallDir 'README.md') -Force
    Copy-Item (Join-Path $repo 'out\version.txt') (Join-Path $InstallDir 'version.txt') -Force
    Copy-Item (Join-Path $repo 'tools\update.ps1') (Join-Path $InstallDir 'update.ps1') -Force
    Copy-Item (Join-Path $repo 'tools\update.cmd') (Join-Path $InstallDir 'update.cmd') -Force
    Unblock-File $targetExe, (Join-Path $InstallDir 'update.ps1'), (Join-Path $InstallDir 'update.cmd')
    Info ("deployed " + $Version + " to " + $InstallDir)
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $InstallDir 'update.ps1') -CheckOnly
} else {
    Warn 'deploy skipped'
}

if ($Push) {
    Step 'git'
    & git -C $repo add -A
    Assert-Exit 'git add'
    & git -C $repo -c user.name='SmartLauncher' -c user.email='smartlauncher@local' commit -m ("release: v" + $Version)
    Assert-Exit 'git commit'
    & git -C $repo push origin main
    Assert-Exit 'git push'
} else {
    Warn 'git push skipped (use -Push)'
}

Write-Host ''
Write-Host ("release " + $Version + " done") -ForegroundColor Green
