[CmdletBinding()]
param(
    [ValidateSet('GitHub', 'S3')]
    [string]$Target = 'GitHub',

    [string]$Version,

    [string]$Repository = $env:GITHUB_REPOSITORY,
    [string]$Branch = 'main',
    [string]$Token = $env:GITHUB_TOKEN,

    [string]$BaseUrl,
    [string]$Bucket = $env:S3_BUCKET,
    [string]$Prefix = 'smartlauncher',
    [string]$Endpoint = $env:S3_ENDPOINT,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repoRoot 'out\SmartLauncherNext.exe'
$buildScript = Join-Path $repoRoot 'build.ps1'
$readme = Join-Path $repoRoot 'README.md'

if (!(Test-Path $exe)) {
    Write-Host "Building SmartLauncherNext..." -ForegroundColor Cyan
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript
    if (!(Test-Path $exe)) { throw "Build failed: $exe not found" }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Item $exe).VersionInfo.ProductVersion
}
$Version = $Version.Trim()
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must look like 1.3.2, got: $Version"
}

$fileVersion = (Get-Item $exe).VersionInfo.ProductVersion
if ($fileVersion -ne $Version) {
    throw "Exe version is $fileVersion, but requested $Version. Build the matching version first."
}

$releaseRoot = Join-Path $repoRoot 'out\release'
$stage = Join-Path $releaseRoot "v$Version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$stageExe = Join-Path $stage 'SmartLauncherNext.exe'
Copy-Item $exe $stageExe -Force
Copy-Item $readme (Join-Path $stage 'README.md') -Force
[System.IO.File]::WriteAllText(
    (Join-Path $stage 'version.txt'),
    $Version,
    (New-Object System.Text.UTF8Encoding($false))
)

$zipName = "SmartLauncherNext-$Version.zip"
$zipPath = Join-Path $releaseRoot $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -Force

$sha256 = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $zipPath).Length

if ($Target -eq 'GitHub') {
    if ([string]::IsNullOrWhiteSpace($Repository)) {
        throw "GitHub target requires -Repository owner/repo (or GITHUB_REPOSITORY)"
    }
    $zipUrl = "https://github.com/$Repository/releases/download/v$Version/$zipName"
    $latestUrl = "https://raw.githubusercontent.com/$Repository/$Branch/latest.json"
}
else {
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        throw "S3 target requires -BaseUrl, e.g. https://cdn.example.com"
    }
    if ([string]::IsNullOrWhiteSpace($Bucket)) {
        throw "S3 target requires -Bucket (or S3_BUCKET)"
    }
    $base = $BaseUrl.TrimEnd('/')
    $keyPrefix = $Prefix.Trim('/')
    $zipUrl = "$base/$keyPrefix/$zipName"
    $latestUrl = "$base/$keyPrefix/latest.json"
}

$latest = [ordered]@{
    version     = $Version
    url         = $zipUrl
    sha256      = $sha256
    size        = $size
    publishedAt = (Get-Date).ToUniversalTime().ToString('o')
}
$latestJson = $latest | ConvertTo-Json -Depth 5
$latestPath = Join-Path $releaseRoot 'latest.json'
[System.IO.File]::WriteAllText(
    $latestPath,
    $latestJson,
    (New-Object System.Text.UTF8Encoding($false))
)

Write-Host "Version:  $Version"
Write-Host "Zip:      $zipPath"
Write-Host "SHA256:   $sha256"
Write-Host "Zip URL:  $zipUrl"
Write-Host "Latest:   $latestUrl"

if ($DryRun) {
    Write-Host "Dry run. Zip contents:" -ForegroundColor Yellow
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $archive.Entries | ForEach-Object { Write-Host ("  " + $_.FullName) }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "latest.json:" -ForegroundColor Yellow
    Write-Host $latestJson
    exit 0
}

if ($Target -eq 'GitHub') {
    if ([string]::IsNullOrWhiteSpace($Token)) { $Token = $env:GH_TOKEN }
    if ([string]::IsNullOrWhiteSpace($Token)) {
        throw "GitHub upload requires GITHUB_TOKEN or GH_TOKEN in the environment"
    }

    $headers = @{
        Authorization          = "Bearer $Token"
        Accept                 = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent'           = 'SmartLauncher-Publisher'
    }
    $api = "https://api.github.com/repos/$Repository"

    $release = $null
    try {
        $release = Invoke-RestMethod -Uri "$api/releases/tags/v$Version" -Headers $headers -Method Get
    }
    catch {
        $status = $null
        if ($_.Exception.Response -ne $null) { $status = $_.Exception.Response.StatusCode.value__ }
        if ($status -ne 404) { throw }
    }

    if ($release -eq $null) {
        $body = @{
            tag_name         = "v$Version"
            target_commitish = $Branch
            name             = "v$Version"
            body             = "SmartLauncher Next $Version"
            draft            = $false
            prerelease       = $false
        } | ConvertTo-Json
        $release = Invoke-RestMethod -Uri "$api/releases" -Method Post -Headers $headers -Body $body -ContentType 'application/json'
    }

    $uploadUrl = $release.upload_url -replace '\{.*$', ''
    foreach ($asset in @($zipPath, $latestPath)) {
        $name = [System.IO.Path]::GetFileName($asset)
        if ($name -like '*.zip') { $contentType = 'application/zip' } else { $contentType = 'application/json' }

        $existing = @($release.assets | Where-Object { $_.name -eq $name })
        foreach ($old in $existing) {
            Invoke-RestMethod -Uri "$api/releases/assets/$($old.id)" -Method Delete -Headers $headers | Out-Null
        }

        $uploadUri = "$uploadUrl?name=$([System.Uri]::EscapeDataString($name))"
        Write-Host "Uploading $name ..."
        try {
            $bytes = [System.IO.File]::ReadAllBytes($asset)
            Invoke-RestMethod -Uri $uploadUri -Method Post -Headers $headers -Body $bytes -ContentType $contentType | Out-Null
        }
        catch {
            $detail = $_.Exception.Message
            if ($_.ErrorDetails -ne $null -and $_.ErrorDetails.Message) {
                $detail += "`n" + $_.ErrorDetails.Message
            }
            throw "Upload failed for $name : $detail"
        }
    }

    $contentsApi = "$api/contents/latest.json"
    $existingSha = $null
    try {
        $existingFile = Invoke-RestMethod -Uri "$contentsApi`?ref=$Branch" -Headers $headers -Method Get
        $existingSha = $existingFile.sha
    }
    catch {
        $status = $null
        if ($_.Exception.Response -ne $null) { $status = $_.Exception.Response.StatusCode.value__ }
        if ($status -ne 404) { throw }
    }

    $contentBody = @{
        message = "release: v$Version"
        content = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($latestJson))
        branch  = $Branch
    }
    if ($existingSha -ne $null) { $contentBody.sha = $existingSha }
    Invoke-RestMethod -Uri $contentsApi -Method Put -Headers $headers -Body ($contentBody | ConvertTo-Json) -ContentType 'application/json' | Out-Null

    Write-Host "Published: $zipUrl" -ForegroundColor Green
    Write-Host "latest.json: $latestUrl" -ForegroundColor Green
}
else {
    $aws = Get-Command aws -ErrorAction SilentlyContinue
    if ($aws -eq $null) {
        throw "S3 target requires the AWS CLI (aws) on PATH. It works with Cloudflare R2, MinIO and S3-compatible endpoints."
    }

    $keyPrefix = $Prefix.Trim('/')
    $zipKey = "s3://$Bucket/$keyPrefix/$zipName"
    $latestKey = "s3://$Bucket/$keyPrefix/latest.json"

    $awsArgs = @('s3', 'cp', $zipPath, $zipKey,
        '--content-type', 'application/zip',
        '--cache-control', 'public,max-age=31536000,immutable',
        '--no-progress')
    if (![string]::IsNullOrWhiteSpace($Endpoint)) { $awsArgs += @('--endpoint-url', $Endpoint) }
    Write-Host "Uploading $zipKey ..."
    & aws @awsArgs
    if ($LASTEXITCODE -ne 0) { throw "aws s3 cp failed for zip" }

    $awsArgs = @('s3', 'cp', $latestPath, $latestKey,
        '--content-type', 'application/json',
        '--cache-control', 'no-cache',
        '--no-progress')
    if (![string]::IsNullOrWhiteSpace($Endpoint)) { $awsArgs += @('--endpoint-url', $Endpoint) }
    Write-Host "Uploading $latestKey ..."
    & aws @awsArgs
    if ($LASTEXITCODE -ne 0) { throw "aws s3 cp failed for latest.json" }

    Write-Host "Published: $zipUrl" -ForegroundColor Green
    Write-Host "latest.json: $latestUrl" -ForegroundColor Green
}
