[CmdletBinding()]
param(
    [string]$InstallDir = 'D:\Tools\SmartLauncher_Next_Lite',
    [string]$LatestUrl = 'https://raw.githubusercontent.com/Qiao-794/SmartLauncherNext/main/latest.json',
    [switch]$Show,
    [switch]$Force,
    [switch]$CheckOnly,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-LocalVersion {
    $vf = Join-Path $InstallDir 'version.txt'
    if (Test-Path $vf) {
        $v = (Get-Content $vf -Raw).Trim()
        if ($v) { return $v }
    }

    $exe = Join-Path $InstallDir 'SmartLauncherNext.exe'
    if (Test-Path $exe) {
        return (Get-Item $exe).VersionInfo.ProductVersion
    }
    return $null
}

try {
    Write-Step "Checking latest release"
    $headers = @{
        'Cache-Control' = 'no-cache'
        'User-Agent'    = 'SmartLauncher-Updater'
    }
    $latest = Invoke-RestMethod -Uri $LatestUrl -Headers $headers -TimeoutSec 60

    $latestVersion = "$($latest.version)".Trim()
    $zipUrl = "$($latest.url)".Trim()
    $expectedHash = "$($latest.sha256)".Trim().ToLowerInvariant()
    $expectedSize = 0
    if ($latest.size -ne $null) { $expectedSize = [long]$latest.size }

    if ([string]::IsNullOrWhiteSpace($latestVersion) -or [string]::IsNullOrWhiteSpace($zipUrl)) {
        throw "latest.json is missing version or url"
    }
    if ($expectedHash -notmatch '^[0-9a-f]{64}$') {
        throw "latest.json has an invalid sha256"
    }

    $localVersion = Get-LocalVersion
    Write-Host "Local:  $localVersion"
    Write-Host "Latest: $latestVersion"
    Write-Host "URL:    $zipUrl"

    if (!$Force -and $localVersion) {
        try {
            if ([version]$localVersion -ge [version]$latestVersion) {
                Write-Step "Already up to date"
                if ($Show -and !$NoLaunch) {
                    Start-Process -FilePath (Join-Path $InstallDir 'SmartLauncherNext.exe') -ArgumentList '--show' -WorkingDirectory $InstallDir
                }
                exit 0
            }
        }
        catch {
            Write-Host "Version comparison failed; continuing with update"
        }
    }

    if ($CheckOnly) {
        Write-Host "Update available: $localVersion -> $latestVersion"
        exit 0
    }

    $work = Join-Path $env:TEMP ("SmartLauncherUpdate-" + [Guid]::NewGuid().ToString('N'))
    $zipPath = Join-Path $work 'release.zip'
    $extractDir = Join-Path $work 'extract'
    New-Item -ItemType Directory -Path $work -Force | Out-Null

    try {
        Write-Step "Downloading $latestVersion"
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing -TimeoutSec 180

        $actualSize = (Get-Item $zipPath).Length
        if ($expectedSize -gt 0 -and $actualSize -ne $expectedSize) {
            throw "Size mismatch: expected $expectedSize, got $actualSize"
        }

        $actualHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash) {
            throw "SHA256 mismatch: expected $expectedHash, got $actualHash"
        }
        Write-Step "SHA256 verified"

        Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force
        $newExe = Join-Path $extractDir 'SmartLauncherNext.exe'
        $newReadme = Join-Path $extractDir 'README.md'
        $newVersionFile = Join-Path $extractDir 'version.txt'
        if (!(Test-Path $newExe)) { throw "Release zip does not contain SmartLauncherNext.exe" }
        if (!(Test-Path $newReadme)) { throw "Release zip does not contain README.md" }

        $newVersion = (Get-Item $newExe).VersionInfo.ProductVersion
        if ($newVersion -ne $latestVersion) {
            throw "Downloaded exe version is $newVersion, expected $latestVersion"
        }

        Write-Step "Stopping running instance"
        Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 500

        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
        $targetExe = Join-Path $InstallDir 'SmartLauncherNext.exe'
        $targetReadme = Join-Path $InstallDir 'README.md'
        $targetVersion = Join-Path $InstallDir 'version.txt'

        if (Test-Path $targetExe) {
            Copy-Item $targetExe ($targetExe + '.bak') -Force
        }

        Write-Step "Deploying $latestVersion"
        Copy-Item $newExe $targetExe -Force
        Copy-Item $newReadme $targetReadme -Force
        if (Test-Path $newVersionFile) {
            Copy-Item $newVersionFile $targetVersion -Force
        }
        else {
            Set-Content -LiteralPath $targetVersion -Value $latestVersion -Encoding Ascii
        }

        # On first install the script is usually run from TEMP; copy it into the install dir.
        # When already running from the install dir, do not copy over the running file.
        $self = $PSCommandPath
        $installedScript = Join-Path $InstallDir 'update.ps1'
        if ($self -and (Test-Path $self) -and ($self -ne $installedScript)) {
            Copy-Item $self $installedScript -Force
        }

        Write-Step "Installed $latestVersion"
        if ($Show -and !$NoLaunch) {
            Start-Process -FilePath $targetExe -ArgumentList '--show' -WorkingDirectory $InstallDir
        }
        exit 0
    }
    finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}
catch {
    Write-Host ("Update failed: " + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
