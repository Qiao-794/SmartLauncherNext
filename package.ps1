<#
  Build a migration package for SmartLauncher Next.

  Output (default D:\Admin\Downloads):
    SmartLauncherNext-migration-<version>.zip
      deploy\                      runnable folder (exe + launcher.json + Cache + README)
      MIGRATION.md                 how to restore / deploy on another machine
      SmartLauncherNext.bundle     full git history (when .git exists)
      SmartLauncherNext-backup.zip source + built exe + config + icons

  Usage:
    powershell -ExecutionPolicy Bypass -File package.ps1
    powershell -ExecutionPolicy Bypass -File package.ps1 -Version 1.3.4 -SkipBuild
    powershell -ExecutionPolicy Bypass -File package.ps1 -OutputDir D:\tmp\pkg

  Notes:
    - launcher.json / Cache come from -DeploySource (machine specific config).
    - the exe always comes from out\ (the build output), so the package never
      ships a stale binary.
#>
param(
    [string]$Version = '',
    [string]$OutputDir = 'D:\Admin\Downloads',
    [string]$DeploySource = 'D:\Tools\SmartLauncher_Next_Lite',
    [switch]$SkipBuild,
    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot

function Step([string]$text) { Write-Host ("== " + $text + " ==") -ForegroundColor Cyan }
function Info([string]$text) { Write-Host ("   " + $text) -ForegroundColor DarkGray }
function Warn([string]$text) { Write-Host ("   WARN " + $text) -ForegroundColor Yellow }

function Find-Git {
    $cmd = Get-Command git -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($p in @('C:\Program Files\Git\cmd\git.exe', 'C:\Program Files (x86)\Git\cmd\git.exe')) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

# ---------- version ----------
if (-not $Version) {
    $viPath = Join-Path $repo 'src\VersionInfo.cs'
    $vi = [System.IO.File]::ReadAllText($viPath)
    $m = [regex]::Match($vi, 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)')
    if (-not $m.Success) { throw "cannot parse version from $viPath" }
    $Version = $m.Groups[1].Value + '.' + $m.Groups[2].Value + '.' + $m.Groups[3].Value
}
Info ("version " + $Version)

# ---------- build ----------
Step 'build'
if ($SkipBuild) {
    Info 'skipped (-SkipBuild)'
} else {
    & (Join-Path $repo 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw ("build failed: " + $LASTEXITCODE) }
}

$exe = Join-Path $repo 'out\SmartLauncherNext.exe'
if (-not (Test-Path $exe)) { throw "missing $exe" }
$exeInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
Info ("exe " + (Get-Item $exe).Length + " bytes, file version " + $exeInfo.FileVersion)
if ($exeInfo.FileVersion -and -not $exeInfo.FileVersion.StartsWith($Version)) {
    Warn ("exe file version " + $exeInfo.FileVersion + " does not match " + $Version)
}

# ---------- staging ----------
$staging = Join-Path $env:TEMP ('sl-package-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Info ("staging " + $staging)

try {
    # ---------- git bundle ----------
    Step 'git bundle'
    $git = Find-Git
    if (-not $git) {
        Warn 'git not found, bundle skipped (source backup still included)'
    } elseif (-not (Test-Path (Join-Path $repo '.git'))) {
        Warn 'no .git in repo, bundle skipped'
    } else {
        $bundle = Join-Path $staging 'SmartLauncherNext.bundle'
        # git 把进度/校验结果写到 stderr；临时放开 Stop，避免被当成终止错误
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            & $git -C $repo bundle create $bundle --all | Out-Null
            $createExit = $LASTEXITCODE
            $verify = (& $git -C $repo bundle verify $bundle 2>&1 | Out-String)
            $verifyExit = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $prevEap
        }
        if ($createExit -ne 0) { throw 'git bundle create failed' }
        if ($verifyExit -ne 0) { throw 'git bundle verify failed' }
        $historyOk = ($verify -match 'complete history')
        Info ("bundle " + [Math]::Round((Get-Item $bundle).Length / 1KB) + " KB, history check " + $(if ($historyOk) { 'passed' } else { 'unknown' }))
    }

    # ---------- deploy folder ----------
    Step 'deploy folder'
    $deploy = Join-Path $staging 'deploy'
    New-Item -ItemType Directory -Force -Path $deploy | Out-Null
    Copy-Item $exe (Join-Path $deploy 'SmartLauncherNext.exe') -Force
    Copy-Item (Join-Path $repo 'README.md') (Join-Path $deploy 'README.md') -Force

    $fromDeploy = 0
    foreach ($name in @('launcher.json', 'launcher.json.bak')) {
        $src = Join-Path $DeploySource $name
        if (Test-Path $src) { Copy-Item $src (Join-Path $deploy $name) -Force; $fromDeploy++ }
    }
    $cacheSrc = Join-Path $DeploySource 'Cache\Icons'
    if (Test-Path $cacheSrc) {
        Copy-Item $cacheSrc (Join-Path $deploy 'Cache\Icons') -Recurse -Force
        $fromDeploy++
    }
    if ($fromDeploy -eq 0) { Warn ("no config found in " + $DeploySource + "; deploy\ has no launcher.json") }
    Info ((Get-ChildItem $deploy -Recurse -File).Count.ToString() + ' files in deploy\')

    # ---------- migration doc ----------
    # 把文档里的版本号同步成这次打包的版本
    $doc = [System.IO.File]::ReadAllText((Join-Path $repo 'MIGRATION.md'))
    $doc = [regex]::Replace($doc, '当前版本：v\d+(\.\d+)*', ('当前版本：v' + $Version))
    [System.IO.File]::WriteAllText((Join-Path $staging 'MIGRATION.md'), $doc, (New-Object System.Text.UTF8Encoding($false)))
    Info ('MIGRATION.md version -> v' + $Version)

    # ---------- source backup ----------
    Step 'source backup'
    $tree = Join-Path $staging 'backup-tree'
    New-Item -ItemType Directory -Force -Path $tree | Out-Null
    foreach ($item in @('.gitignore', 'app.ico', 'app.manifest', 'app.png', 'build.ps1', 'package.ps1',
                        'CHANGELOG.md', 'README.md', 'MIGRATION.md', 'src', 'tests', 'docs')) {
        $src = Join-Path $repo $item
        if (Test-Path $src) { Copy-Item $src (Join-Path $tree $item) -Recurse -Force }
    }
    Copy-Item $exe (Join-Path $tree 'SmartLauncherNext.exe') -Force
    if (Test-Path (Join-Path $deploy 'launcher.json')) {
        Copy-Item (Join-Path $deploy 'launcher.json') (Join-Path $tree 'launcher.json') -Force
    }
    if (Test-Path (Join-Path $deploy 'Cache')) {
        Copy-Item (Join-Path $deploy 'Cache') (Join-Path $tree 'Cache') -Recurse -Force
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $backupZip = Join-Path $staging 'SmartLauncherNext-backup.zip'
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tree, $backupZip)
    Remove-Item $tree -Recurse -Force
    Info ("backup zip " + [Math]::Round((Get-Item $backupZip).Length / 1KB) + " KB")

    # ---------- final zip ----------
    Step 'package'
    if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null }
    $zipPath = Join-Path $OutputDir ('SmartLauncherNext-migration-' + $Version + '.zip')
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    $entries = $archive.Entries.Count
    $archive.Dispose()

    $exeHash = (Get-FileHash $exe -Algorithm SHA256).Hash
    $zipHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash

    Write-Host ''
    Write-Host 'package ready' -ForegroundColor Green
    Write-Host ("  zip     : " + $zipPath)
    Write-Host ("  size    : " + [Math]::Round((Get-Item $zipPath).Length / 1MB, 2) + " MB, " + $entries + " entries")
    Write-Host ("  version : " + $Version)
    Write-Host ("  exe     : sha256 " + $exeHash)
    Write-Host ("  zip     : sha256 " + $zipHash)
}
finally {
    if ($KeepStaging) {
        Write-Host ("staging kept: " + $staging) -ForegroundColor Yellow
    } else {
        Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}
