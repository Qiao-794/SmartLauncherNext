$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $fx 'WPF'
$outDir = Join-Path $root 'out'
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$refs = @(
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $fx 'System.Xaml.dll'),
    (Join-Path $fx 'System.Web.Extensions.dll'),
    (Join-Path $fx 'System.Drawing.dll')
)

$sources = Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName }

$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    '/utf8output',
    "/out:$(Join-Path $outDir 'SmartLauncherNext.exe')",
    "/win32manifest:$(Join-Path $root 'app.manifest')",
    "/win32icon:$(Join-Path $root 'app.ico')"
)
foreach ($r in $refs) { $args += "/r:$r" }
$args += $sources

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "compile failed: $LASTEXITCODE" }

$data = Join-Path $outDir 'launcher.json'
$icons = Join-Path $outDir 'Cache\Icons'
if (!(Test-Path $data)) {
    $legacy = 'D:\Program Files\SmartLauncher_Lite'
    if (Test-Path (Join-Path $legacy 'launcher.json')) {
        Copy-Item (Join-Path $legacy 'launcher.json') $data
        if (Test-Path (Join-Path $legacy 'Cache\Icons')) {
            New-Item -ItemType Directory -Force -Path $icons | Out-Null
            Copy-Item (Join-Path $legacy 'Cache\Icons\*.png') $icons -Force
        }
        Write-Host 'legacy config + icons copied to out'
    }
}
Write-Host ("built: " + (Join-Path $outDir 'SmartLauncherNext.exe'))
