[CmdletBinding()]
param(
    [string]$InstallDir = 'D:\Tools\SmartLauncher_Next_Lite',
    [string]$LatestUrl = 'https://raw.githubusercontent.com/Qiao-794/SmartLauncherNext/main/latest.json',
    [switch]$Show,
    [switch]$Force,
    [switch]$CheckOnly,
    [switch]$NoLaunch,
    [switch]$Rollback
)

# Exit codes: 0 ok / 2 network / 3 locked / 4 download+verify / 5 install verify / 6 no backup
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$targetExe     = Join-Path $InstallDir 'SmartLauncherNext.exe'
$targetReadme  = Join-Path $InstallDir 'README.md'
$targetVersion = Join-Path $InstallDir 'version.txt'
$backupExe     = $targetExe + '.bak'
$logPath       = Join-Path $InstallDir 'update.log'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  ' + $Level.PadRight(5) + '  ' + $Message
    Write-Host $line
    try {
        if (!(Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }
        [System.IO.File]::AppendAllText($logPath, $line + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
    }
    catch {
    }
}

function Limit-Log {
    try {
        if (Test-Path $logPath) {
            if ((Get-Item $logPath).Length -gt 256KB) {
                $tail = Get-Content $logPath -Tail 400
                [System.IO.File]::WriteAllLines($logPath, $tail, (New-Object System.Text.UTF8Encoding($false)))
            }
        }
    }
    catch {
    }
}

function Get-ExeVersion([string]$path) {
    try {
        if (Test-Path $path) { return (Get-Item $path).VersionInfo.ProductVersion }
    }
    catch {
    }
    return $null
}

function Get-TextVersion([string]$dir) {
    try {
        $f = Join-Path $dir 'version.txt'
        if (Test-Path $f) {
            $v = (Get-Content $f -Raw).Trim()
            if ($v) { return $v }
        }
    }
    catch {
    }
    return $null
}

function Write-VersionFile([string]$version) {
    [System.IO.File]::WriteAllText($targetVersion, $version, (New-Object System.Text.UTF8Encoding($false)))
}

function Stop-InstalledApp {
    # Only stop the instance that belongs to this install directory.
    $stopped = 0
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue | ForEach-Object {
        $proc = $_
        $path = $null
        try { $path = $proc.Path } catch { }
        if ($path -and ($path -ieq $targetExe)) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            $stopped++
        }
    }
    if ($stopped -gt 0) { Start-Sleep -Milliseconds 500 }
    return $stopped
}

function Start-Panel {
    if ($Show -and !$NoLaunch) {
        Start-Process -FilePath $targetExe -ArgumentList '--show' -WorkingDirectory $InstallDir
    }
}

function Invoke-Update {
    Write-Log ("update started (install=" + $InstallDir + ")")
    Limit-Log

    if ($Rollback) {
        if (!(Test-Path $backupExe)) {
            Write-Log 'no backup exe found, cannot roll back' 'ERROR'
            return 6
        }
        $null = Stop-InstalledApp
        Copy-Item $backupExe $targetExe -Force
        $v = Get-ExeVersion $targetExe
        if ($v) { Write-VersionFile $v }
        Write-Log ("rolled back to " + $v)
        Start-Panel
        return 0
    }

    # The exe is the source of truth; repair version.txt when they disagree.
    $exeVersion = Get-ExeVersion $targetExe
    $txtVersion = Get-TextVersion $InstallDir
    if ($exeVersion -and $txtVersion -and ($exeVersion -ne $txtVersion)) {
        Write-Log ("version.txt (" + $txtVersion + ") does not match exe (" + $exeVersion + "), trusting the exe") 'WARN'
        Write-VersionFile $exeVersion
        $txtVersion = $exeVersion
    }
    $localVersion = $exeVersion
    if (-not $localVersion) { $localVersion = $txtVersion }

    $headers = @{
        'Cache-Control' = 'no-cache'
        'User-Agent'    = 'SmartLauncher-Updater'
    }

    try {
        $latest = Invoke-RestMethod -Uri $LatestUrl -Headers $headers -TimeoutSec 60
    }
    catch {
        Write-Log ("cannot reach update source: " + $_.Exception.Message) 'ERROR'
        return 2
    }

    $latestVersion = "$($latest.version)".Trim()
    $zipUrl = "$($latest.url)".Trim()
    $expectedHash = "$($latest.sha256)".Trim().ToLowerInvariant()
    $expectedSize = 0
    if ($latest.size -ne $null) { $expectedSize = [long]$latest.size }

    if ([string]::IsNullOrWhiteSpace($latestVersion) -or [string]::IsNullOrWhiteSpace($zipUrl)) {
        Write-Log 'latest.json is missing version or url' 'ERROR'
        return 2
    }
    if ($expectedHash -notmatch '^[0-9a-f]{64}$') {
        Write-Log 'latest.json has an invalid sha256' 'ERROR'
        return 2
    }

    Write-Host "Local:  $localVersion"
    Write-Host "Latest: $latestVersion"
    Write-Host "URL:    $zipUrl"

    if (!$Force -and $localVersion) {
        try {
            if ([version]$localVersion -ge [version]$latestVersion) {
                Write-Log 'already up to date'
                Start-Panel
                return 0
            }
        }
        catch {
            Write-Log 'version comparison failed, continuing with update' 'WARN'
        }
    }

    if ($CheckOnly) {
        Write-Log ("update available: " + $localVersion + " -> " + $latestVersion)
        return 0
    }

    $work = Join-Path $env:TEMP ('SmartLauncherUpdate-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    try {
        $zipPath = Join-Path $work 'release.zip'
        $extractDir = Join-Path $work 'extract'

        try {
            Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing -TimeoutSec 180
        }
        catch {
            Write-Log ("download failed: " + $_.Exception.Message) 'ERROR'
            return 4
        }

        if ($expectedSize -gt 0) {
            $actualSize = (Get-Item $zipPath).Length
            if ($actualSize -ne $expectedSize) {
                Write-Log ("size mismatch: expected $expectedSize, got $actualSize") 'ERROR'
                return 4
            }
        }

        $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) {
            Write-Log ("sha256 mismatch: expected $expectedHash, got $actualHash") 'ERROR'
            return 4
        }
        Write-Log 'sha256 verified'

        Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force
        $newExe = Join-Path $extractDir 'SmartLauncherNext.exe'
        $newReadme = Join-Path $extractDir 'README.md'
        $newVersionFile = Join-Path $extractDir 'version.txt'
        if (!(Test-Path $newExe)) { Write-Log 'release zip has no SmartLauncherNext.exe' 'ERROR'; return 4 }
        if (!(Test-Path $newReadme)) { Write-Log 'release zip has no README.md' 'ERROR'; return 4 }

        $newVersion = Get-ExeVersion $newExe
        if ($newVersion -ne $latestVersion) {
            Write-Log ("downloaded exe is $newVersion, expected $latestVersion") 'ERROR'
            return 4
        }

        $stopped = Stop-InstalledApp
        Write-Log ("stopped instances: " + $stopped)

        if (Test-Path $targetExe) {
            Copy-Item $targetExe $backupExe -Force
            Write-Log ("backup -> " + $backupExe)
        }

        Copy-Item $newExe $targetExe -Force
        Copy-Item $newReadme $targetReadme -Force
        if (Test-Path $newVersionFile) { Copy-Item $newVersionFile $targetVersion -Force }
        else { Write-VersionFile $latestVersion }

        $installed = Get-ExeVersion $targetExe
        if ($installed -ne $latestVersion) {
            Write-Log ("post-install check failed: exe is $installed, expected $latestVersion; restoring backup") 'ERROR'
            if (Test-Path $backupExe) {
                Copy-Item $backupExe $targetExe -Force
                $restored = Get-ExeVersion $targetExe
                if ($restored) { Write-VersionFile $restored }
                Write-Log ("restored " + $restored)
            }
            return 5
        }

        $self = $PSCommandPath
        $installedScript = Join-Path $InstallDir 'update.ps1'
        if ($self -and (Test-Path $self) -and ($self -ne $installedScript)) {
            Copy-Item $self $installedScript -Force
        }

        $installedCmd = Join-Path $InstallDir 'update.cmd'
        if (!(Test-Path $installedCmd)) {
            $cmdText = "@echo off`r`nsetlocal`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0update.ps1`" %*`r`nif errorlevel 1 pause`r`nexit /b %errorlevel%`r`n"
            [System.IO.File]::WriteAllText($installedCmd, $cmdText, (New-Object System.Text.ASCIIEncoding))
        }

        Write-Log ("installed " + $latestVersion)
        Start-Panel
        return 0
    }
    catch {
        Write-Log ("update failed: " + $_.Exception.Message) 'ERROR'
        return 1
    }
    finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$mutex = New-Object System.Threading.Mutex($false, 'Local\SmartLauncherNext.Update')
if (-not $mutex.WaitOne(0)) {
    Write-Log 'another update is already running' 'WARN'
    exit 3
}

$exitCode = 1
try {
    $exitCode = Invoke-Update
}
catch {
    Write-Log ("unexpected error: " + $_.Exception.Message) 'ERROR'
    $exitCode = 1
}
finally {
    try { $mutex.ReleaseMutex() } catch { }
    $mutex.Dispose()
}

exit $exitCode
