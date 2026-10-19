param(
    [string]$TestDir = 'D:\Dev\Deepseek-00\.tmp\smoketest',
    [string]$SourceDir = 'D:\Dev\Deepseek-00\SmartLauncherNext'
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $TestDir 'SmartLauncherNext.exe'
$instance = '--instance=smoke'
$log = Join-Path $TestDir 'debug.log'

if (!(Test-Path $TestDir)) { New-Item -ItemType Directory -Path $TestDir | Out-Null }
Copy-Item (Join-Path $SourceDir 'out\SmartLauncherNext.exe') $exe -Force
if (!(Test-Path (Join-Path $TestDir 'launcher.json'))) {
    Copy-Item (Join-Path $SourceDir 'out\launcher.json') $TestDir -Force
}
if (!(Test-Path (Join-Path $TestDir 'Cache'))) {
    Copy-Item (Join-Path $SourceDir 'out\Cache') $TestDir -Recurse -Force
}
Remove-Item $log -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class SmokeWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  public static IntPtr Find(uint pid, string title) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p;
      GetWindowThreadProcessId(h, out p);
      if (p == pid) {
        StringBuilder t = new StringBuilder(256);
        GetWindowText(h, t, 256);
        if (t.ToString() == title) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@

function Get-App {
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $exe } catch { $false } } |
        Select-Object -First 1
}

function Wait-App([int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $p = Get-App
        if ($p) { return $p }
        Start-Sleep -Milliseconds 25
    }
    return $null
}

function Wait-Window([int]$appPid, [string]$title, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $h = [SmokeWin]::Find([uint32]$appPid, $title)
        if ($h -ne [IntPtr]::Zero -and [SmokeWin]::IsWindowVisible($h)) { return $h }
        Start-Sleep -Milliseconds 500
    }
    return [IntPtr]::Zero
}

function Fail([string]$message) {
    Write-Host "SMOKE FAIL: $message"
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $exe } catch { $false } } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    exit 1
}

try {
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $exe } catch { $false } } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
    Remove-Item $log -ErrorAction SilentlyContinue

    cmd.exe /c start "" "$exe" --show --pin --light $instance
    $p = Wait-App 6000
    if (-not $p) { Fail 'process did not start' }
    $panel = Wait-Window $p.Id 'SmartLauncherNextPanel' 6000
    if ($panel -eq [IntPtr]::Zero) { Fail 'panel not visible' }
    Start-Sleep -Milliseconds 500

    $rect = New-Object SmokeWin+RECT
    [SmokeWin]::GetWindowRect($panel, [ref]$rect) | Out-Null
    $pw = $rect.R - $rect.L
    $ph = $rect.B - $rect.T
    $blank = $true
    if ($pw -gt 100 -and $ph -gt 100) {
        $bmp = New-Object System.Drawing.Bitmap($pw, $ph)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($rect.L, $rect.T, 0, 0, $bmp.Size)
        $dark = 0
        for ($y = 0; $y -lt $ph; $y += 2) {
            for ($x = 0; $x -lt $pw; $x += 2) {
                $c = $bmp.GetPixel($x, $y)
                if ($c.R -lt 120 -or $c.G -lt 120 -or $c.B -lt 120) { $dark++ }
            }
        }
        $g.Dispose()
        $bmp.Dispose()
        if ($dark -ge 20) { $blank = $false }
    }
    if ($blank) { Fail 'panel content appears blank' }

    $text = Get-Content $log -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($text -notmatch 'hooks installed=True') { Fail 'keyboard hook was not installed' }

    [SmokeWin]::SetForegroundWindow($panel) | Out-Null
    Start-Sleep -Milliseconds 250
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt 3000 -and [SmokeWin]::IsWindowVisible($panel)) {
        Start-Sleep -Milliseconds 25
    }
    if ([SmokeWin]::IsWindowVisible($panel)) { Fail 'Esc did not hide the panel' }
    Start-Sleep -Milliseconds 500

    cmd.exe /c start "" "$exe" --settings --light $instance
    $settings = Wait-Window $p.Id 'SmartLauncher 设置' 6000
    if ($settings -eq [IntPtr]::Zero) { Fail 'settings window not visible' }
    [SmokeWin]::PostMessage($settings, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Milliseconds 500

    $text = Get-Content $log -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($text -match 'Exception|FATAL|Dispatcher:|AppDomain') { Fail 'errors found in debug.log' }

    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $exe } catch { $false } } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host 'SMOKE PASS'
    exit 0
}
catch {
    Fail $_.Exception.Message
}
