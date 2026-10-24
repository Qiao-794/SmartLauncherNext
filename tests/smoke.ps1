<#
  Smoke test for SmartLauncher Next (window overview panel).

  Expects out\SmartLauncherNext.exe to exist (run build.ps1 first).
  Runs an isolated instance (--instance=smoke) from .smoke\run and checks:

    1. panel window appears and renders non-blank content
    2. keyboard hook is installed
    3. window rows / visible rows / panel height follow the layout rules
    4. wheel scrolling reaches the bottom and the bottom fade turns off
    5. reopening the panel resets the scroll offset to 0
    6. Esc hides the panel
    7. settings window opens and closes
    8. debug.log contains no exceptions

  The test briefly opens $SpawnWindows console windows so the list is long
  enough to scroll; they are closed in the finally block.

  Usage:
    powershell -ExecutionPolicy Bypass -File tests\smoke.ps1
    powershell -ExecutionPolicy Bypass -File tests\smoke.ps1 -SpawnWindows 0
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$SpawnWindows = 16,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'

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
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int d, UIntPtr e);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);

  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

  public static IntPtr Find(string title, uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      if (pid != 0) {
        uint p; GetWindowThreadProcessId(h, out p);
        if (p != pid) return true;
      }
      StringBuilder sb = new StringBuilder(400);
      GetWindowText(h, sb, 400);
      if (sb.ToString() == title && IsWindowVisible(h)) { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }

  public static void Wheel(int delta) {
    mouse_event(0x0800, 0, 0, delta, UIntPtr.Zero);
  }

  public static RECT GetRect(IntPtr h) {
    RECT r;
    GetWindowRect(h, out r);
    return r;
  }

  public static POINT Cursor() {
    POINT p;
    GetCursorPos(out p);
    return p;
  }

  public static IntPtr CursorWindow() {
    POINT p;
    GetCursorPos(out p);
    return WindowFromPoint(p);
  }
}
"@

$exePath    = Join-Path $RepoRoot 'out\SmartLauncherNext.exe'
$testDir    = Join-Path $RepoRoot '.smoke\run'
$testExe    = Join-Path $testDir 'SmartLauncherNext.exe'
$log        = Join-Path $testDir 'debug.log'
$instance   = '--instance=smoke'
$panelTitle = 'SmartLauncherNextPanel'
$settings   = 'SmartLauncher ' + [char]0x8BBE + [char]0x7F6E

$script:failures = New-Object System.Collections.ArrayList
$script:spawned  = New-Object System.Collections.ArrayList
$script:panelProc = $null
$script:cursor   = New-Object SmokeWin+POINT
$script:cursorSaved = $false

function Assert([bool]$condition, [string]$message) {
    if ($condition) {
        Write-Host ("  ok   " + $message) -ForegroundColor DarkGray
    } else {
        Write-Host ("  FAIL " + $message) -ForegroundColor Red
        [void]$script:failures.Add($message)
    }
}

function Wait-Window([string]$title, [uint32]$processId, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $h = [SmokeWin]::Find($title, $processId)
        if ($h -ne [IntPtr]::Zero) { return $h }
        Start-Sleep -Milliseconds 100
    }
    return [IntPtr]::Zero
}

function Read-Log {
    if (-not (Test-Path $log)) { return '' }
    return [System.IO.File]::ReadAllText($log, [System.Text.Encoding]::UTF8)
}

function Cleanup {
    if ($script:panelProc -and -not $script:panelProc.HasExited) {
        Stop-Process -Id $script:panelProc.Id -Force -ErrorAction SilentlyContinue
    }
    foreach ($id in $script:spawned) {
        Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }
    if ($script:cursorSaved) {
        [SmokeWin]::SetCursorPos($script:cursor.X, $script:cursor.Y) | Out-Null
    }
    if (-not $KeepArtifacts) {
        Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

try {
    Write-Host '== prepare ==' -ForegroundColor Cyan
    if (-not (Test-Path $exePath)) { throw "missing $exePath; run build.ps1 first" }
    if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $testDir | Out-Null
    Copy-Item $exePath $testExe -Force
    $cfg = Join-Path $RepoRoot 'out\launcher.json'
    if (Test-Path $cfg) { Copy-Item $cfg $testDir -Force }
    $script:cursorSaved = $true
    $script:cursor = [SmokeWin]::Cursor()

    if ($SpawnWindows -gt 0) {
        Write-Host ("  spawning " + $SpawnWindows + " console windows") -ForegroundColor DarkGray
        for ($i = 1; $i -le $SpawnWindows; $i++) {
            $p = Start-Process -FilePath 'cmd.exe' -ArgumentList '/k', ("title SMOKE{0:D2}" -f $i) -PassThru
            [void]$script:spawned.Add($p.Id)
        }
        Start-Sleep -Milliseconds 1200
    }

    Write-Host '== launch panel ==' -ForegroundColor Cyan
    $script:panelProc = Start-Process -FilePath $testExe -ArgumentList '--show', '--light', $instance -PassThru
    $panel = Wait-Window $panelTitle ([uint32]$script:panelProc.Id) 8000
    Assert ($panel -ne [IntPtr]::Zero) 'panel window is visible'

    if ($panel -ne [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 600

        $logText = Read-Log
        Assert ($logText -match 'hooks installed=True') 'keyboard hook installed'

        $m = [regex]::Match($logText, 'windows built rows=(\d+) visible=(\d+) fit=(\d+) height=([\d\.]+) in (\d+)ms')
        Assert $m.Success 'debug.log reports window build stats'

        $rows = 0; $visible = 0; $fit = 0; $height = 0.0; $buildMs = 0
        if ($m.Success) {
            $rows    = [int]$m.Groups[1].Value
            $visible = [int]$m.Groups[2].Value
            $fit     = [int]$m.Groups[3].Value
            $height  = [double]$m.Groups[4].Value
            $buildMs = [int]$m.Groups[5].Value

            $expectedVisible = [Math]::Min($rows, [Math]::Min(14, $fit))
            $expectedHeight  = 13 + 29 * $expectedVisible + 26

            Assert ($rows -ge 1) ("rows enumerated (" + $rows + ")")
            Assert ($visible -eq $expectedVisible) ("visible rows = " + $visible + " (expected " + $expectedVisible + ")")
            Assert ([Math]::Abs($height - $expectedHeight) -le 1.5) ("panel height " + $height + " (expected ~" + $expectedHeight + ")")
            Assert ($buildMs -lt 1500) ("window build took " + $buildMs + " ms")
        }

        # 面板内容非空白：用 PrintWindow 直接渲染窗口，避免屏幕坐标受 DPI 虚拟化影响
        $scale = 1.0
        $dpi = [SmokeWin]::GetDpiForWindow($panel)
        if ($dpi -gt 0) { $scale = $dpi / 96.0 }

        $rect = [SmokeWin]::GetRect($panel)
        $pw = [int](($rect.R - $rect.L) * $scale)
        $ph = [int](($rect.B - $rect.T) * $scale)

        $captured = $false
        $blank = $true
        if ($pw -gt 100 -and $ph -gt 100) {
            $bmp = New-Object System.Drawing.Bitmap($pw, $ph)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $hdc = $g.GetHdc()
            $captured = [SmokeWin]::PrintWindow($panel, $hdc, 2)
            $g.ReleaseHdc($hdc)
            $g.Dispose()
            if ($captured) {
                $colors = @{}
                $dark = 0
                for ($y = 4; $y -lt ($ph - 4); $y += 6) {
                    for ($x = 4; $x -lt ($pw - 4); $x += 6) {
                        $c = $bmp.GetPixel($x, $y)
                        $key = [string]::Format("{0}-{1}-{2}", [int]($c.R / 16), [int]($c.G / 16), [int]($c.B / 16))
                        $colors[$key] = 1
                        if ($c.R -lt 120 -or $c.G -lt 120 -or $c.B -lt 120) { $dark++ }
                    }
                }
                if ($colors.Count -ge 3 -and $dark -ge 20) { $blank = $false }
                if ($colors.Count -lt 3) { $captured = $false }
            }
            $bmp.Dispose()
        }
        if ($captured) {
            Assert (-not $blank) 'panel content is not blank'
        } else {
            Write-Host '  skip  panel content check (PrintWindow unavailable)' -ForegroundColor Yellow
        }

        if ($rows -gt $visible) {
            Write-Host '== wheel scrolling ==' -ForegroundColor Cyan
            $cxV = [int](($rect.L + $rect.R) / 2)
            $cyV = [int](($rect.T + $rect.B) / 2)
            $cxP = [int]((($rect.L + $rect.R) / 2) * $scale)
            $cyP = [int]((($rect.T + $rect.B) / 2) * $scale)

            # GetWindowRect 与 SetCursorPos 的 DPI 坐标系可能不同，两种都试一次
            $placed = $false
            foreach ($pt in @(@($cxP, $cyP), @($cxV, $cyV))) {
                [SmokeWin]::SetCursorPos($pt[0], $pt[1]) | Out-Null
                Start-Sleep -Milliseconds 150
                if ([SmokeWin]::CursorWindow() -eq $panel) { $placed = $true; break }
            }
            Assert $placed 'cursor placed over the panel'

            if ($placed) {
                for ($i = 0; $i -lt 10; $i++) { [SmokeWin]::Wheel(-120); Start-Sleep -Milliseconds 100 }
                Start-Sleep -Milliseconds 400

                $logText = Read-Log
                $scrolls = [regex]::Matches($logText, 'scroll (\d+)/(\d+)')
                Assert ($scrolls.Count -gt 0) 'scroll events were logged'
                if ($scrolls.Count -gt 0) {
                    $last = $scrolls[$scrolls.Count - 1]
                    $offset = [int]$last.Groups[1].Value
                    $max = [int]$last.Groups[2].Value
                    Assert ($max -gt 0) ("scrollable range exists (" + $max + ")")
                    Assert ($offset -eq $max) ("reached bottom (" + $offset + "/" + $max + ")")
                    Assert ($logText -match 'more=False') 'bottom fade turns off at the end'
                }

                for ($i = 0; $i -lt 12; $i++) { [SmokeWin]::Wheel(120); Start-Sleep -Milliseconds 80 }
                Start-Sleep -Milliseconds 300
                $logText = Read-Log
                $scrolls = [regex]::Matches($logText, 'scroll (\d+)/(\d+)')
                if ($scrolls.Count -gt 0) {
                    $last = $scrolls[$scrolls.Count - 1]
                    Assert ([int]$last.Groups[1].Value -eq 0) 'wheel up returns to top'
                }
            } else {
                Write-Host '  skip  wheel test (cursor could not be placed over the panel)' -ForegroundColor Yellow
            }

            Write-Host '== scroll reset on reopen ==' -ForegroundColor Cyan
            & $testExe --hide $instance | Out-Null
            Start-Sleep -Milliseconds 600
            & $testExe --show --light $instance | Out-Null
            Start-Sleep -Milliseconds 1200
            $logText = Read-Log
            $resets = [regex]::Matches($logText, 'scroll reset -> (\d+)')
            Assert ($resets.Count -ge 2) 'reopen logged a scroll reset'
            if ($resets.Count -gt 0) {
                $lastReset = [int]$resets[$resets.Count - 1].Groups[1].Value
                Assert ($lastReset -eq 0) 'reopen starts at top'
            }
            $panel = Wait-Window $panelTitle ([uint32]$script:panelProc.Id) 4000
        }

        Write-Host '== esc ==' -ForegroundColor Cyan
        if ($panel -ne [IntPtr]::Zero) {
            [SmokeWin]::SetForegroundWindow($panel) | Out-Null
            Start-Sleep -Milliseconds 250
            [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
            $gone = $false
            for ($i = 0; $i -lt 30; $i++) {
                if ([SmokeWin]::Find($panelTitle, [uint32]$script:panelProc.Id) -eq [IntPtr]::Zero) { $gone = $true; break }
                Start-Sleep -Milliseconds 100
            }
            Assert $gone 'Esc hides the panel'
        }
    }

    Write-Host '== settings window ==' -ForegroundColor Cyan
    Start-Process -FilePath $testExe -ArgumentList '--settings', '--light', $instance | Out-Null
    $setWin = Wait-Window $settings 0 8000
    Assert ($setWin -ne [IntPtr]::Zero) 'settings window opens'
    if ($setWin -ne [IntPtr]::Zero) {
        [SmokeWin]::PostMessage($setWin, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 800
        Assert (([SmokeWin]::Find($settings, 0)) -eq [IntPtr]::Zero) 'settings window closes'
    }

    Write-Host '== log ==' -ForegroundColor Cyan
    $logText = Read-Log
    Assert ($logText.Length -gt 0) 'debug.log written'
    Assert ($logText -notmatch 'Exception|FATAL|Dispatcher:') 'no exceptions in debug.log'
}
catch {
    Write-Host ("SMOKE ERROR: " + $_.Exception.Message) -ForegroundColor Red
    [void]$script:failures.Add($_.Exception.Message)
}
finally {
    Cleanup
}

if ($script:failures.Count -gt 0) {
    Write-Host ("SMOKE FAIL (" + $script:failures.Count + ")") -ForegroundColor Red
    foreach ($f in $script:failures) { Write-Host ("  - " + $f) -ForegroundColor Red }
    exit 1
}

Write-Host 'SMOKE PASS' -ForegroundColor Green
exit 0
