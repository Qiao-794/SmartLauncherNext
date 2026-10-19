param(
    [int]$Cycles = 20,
    [string]$TestDir = 'D:\Dev\Deepseek-00\.tmp\stabilitytest'
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $TestDir 'SmartLauncherNext.exe'
$instance = '--instance=stability'
$log = Join-Path $TestDir 'debug.log'
$outCsv = Join-Path $TestDir 'stability-results.csv'

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class StabWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
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

function Get-TestProcess {
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $exe } catch { $false } } |
        Select-Object -First 1
}

function Wait-TestProcess([int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $p = Get-TestProcess
        if ($p) { return $p }
        Start-Sleep -Milliseconds 25
    }
    return $null
}

function Wait-Window([int]$appPid, [string]$title, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $h = [StabWin]::Find([uint32]$appPid, $title)
        if ($h -ne [IntPtr]::Zero -and [StabWin]::IsWindowVisible($h)) { return $h }
        Start-Sleep -Milliseconds 25
    }
    return [IntPtr]::Zero
}

function Wait-Hidden([IntPtr]$h, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if (-not [StabWin]::IsWindowVisible($h)) { return $true }
        Start-Sleep -Milliseconds 25
    }
    return $false
}

function Get-Metrics($p, [int]$cpuMs = 1000) {
    try {
        $p.Refresh()
        $ws = [math]::Round($p.WorkingSet64 / 1MB, 1)
        $priv = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        $handles = $p.HandleCount
        $threads = $p.Threads.Count
        $t0 = $p.TotalProcessorTime
        Start-Sleep -Milliseconds $cpuMs
        $p.Refresh()
        $t1 = $p.TotalProcessorTime
        $delta = ($t1 - $t0).TotalMilliseconds
        $cpu = [math]::Round(($delta / $cpuMs) * 100 / [Environment]::ProcessorCount, 2)
        return [pscustomobject]@{ WS = $ws; Private = $priv; Handles = $handles; Threads = $threads; Cpu = $cpu }
    }
    catch {
        return $null
    }
}

function Read-LogInfo {
    if (-not (Test-Path $log)) { return @{ Hooks = $false; Errors = 0 } }
    $text = Get-Content $log -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($null -eq $text) { $text = '' }
    $hooks = $text -match 'hooks installed=True'
    $errors = ([regex]::Matches($text, 'Exception|FATAL|Dispatcher:|AppDomain')).Count
    return @{ Hooks = $hooks; Errors = $errors }
}

# Make sure no previous instance (production or test) is holding hooks/windows.
Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Remove-Item $outCsv -ErrorAction SilentlyContinue

$rows = New-Object System.Collections.Generic.List[object]

for ($i = 1; $i -le $Cycles; $i++) {
    Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue |
        Where-Object { try { $_.Path -eq $exe } catch { $false } } |
        Stop-Process -Force
    Start-Sleep -Milliseconds 300
    Remove-Item $log -ErrorAction SilentlyContinue

    $cold = 0
    $startupCpu = 0
    $shown = $null
    $hidden = $null
    $settings = $null
    $after = $null
    $appPid = 0
    $panelH = [IntPtr]::Zero
    $settingsH = [IntPtr]::Zero
    $launchOk = $false
    $settingsOk = $false
    $err = ''

    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        cmd.exe /c start "" "$exe" --show --pin $instance
        $p = Wait-TestProcess 6000
        if (-not $p) { throw 'process not started' }
        $appPid = $p.Id
        $panelH = Wait-Window $appPid 'SmartLauncherNextPanel' 6000
        if ($panelH -eq [IntPtr]::Zero) { throw 'panel not visible' }
        $sw.Stop()
        $cold = $sw.ElapsedMilliseconds
        $p.Refresh()
        $startupCpu = [math]::Round($p.TotalProcessorTime.TotalMilliseconds, 0)

        Start-Sleep -Milliseconds 800
        $shown = Get-Metrics $p 1000

        [System.Windows.Forms.SendKeys]::SendWait('d')
        $launchOk = Wait-Hidden $panelH 2500
        if (-not $launchOk) {
            [System.Windows.Forms.SendKeys]::SendWait('d')
            $launchOk = Wait-Hidden $panelH 2000
        }
        Start-Sleep -Milliseconds 300
        $hidden = Get-Metrics $p 1000

        cmd.exe /c start "" "$exe" --settings $instance
        $settingsH = Wait-Window $appPid 'SmartLauncher 设置' 6000
        if ($settingsH -eq [IntPtr]::Zero) { throw 'settings window not visible' }
        Start-Sleep -Milliseconds 600
        $settings = Get-Metrics $p 1000
        [StabWin]::PostMessage($settingsH, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        $closed = Wait-Hidden $settingsH 3000
        if (-not $closed) { throw 'settings did not close' }
        $settingsOk = $true
        Start-Sleep -Milliseconds 500
        $after = Get-Metrics $p 1000
    }
    catch {
        $err = $_.Exception.Message
    }

    $info = Read-LogInfo
    $shownWS = if ($null -ne $shown) { $shown.WS } else { $null }
    $shownPriv = if ($null -ne $shown) { $shown.Private } else { $null }
    $shownCpu = if ($null -ne $shown) { $shown.Cpu } else { $null }
    $shownHandles = if ($null -ne $shown) { $shown.Handles } else { $null }
    $shownThreads = if ($null -ne $shown) { $shown.Threads } else { $null }
    $hiddenWS = if ($null -ne $hidden) { $hidden.WS } else { $null }
    $hiddenPriv = if ($null -ne $hidden) { $hidden.Private } else { $null }
    $hiddenCpu = if ($null -ne $hidden) { $hidden.Cpu } else { $null }
    $settingsWS = if ($null -ne $settings) { $settings.WS } else { $null }
    $settingsPriv = if ($null -ne $settings) { $settings.Private } else { $null }
    $settingsCpu = if ($null -ne $settings) { $settings.Cpu } else { $null }
    $afterWS = if ($null -ne $after) { $after.WS } else { $null }
    $afterPriv = if ($null -ne $after) { $after.Private } else { $null }
    $afterCpu = if ($null -ne $after) { $after.Cpu } else { $null }

    $row = [pscustomobject]@{
        Cycle = $i
        ColdStartMs = $cold
        StartupCpuMs = $startupCpu
        ShownWS = $shownWS
        ShownPrivate = $shownPriv
        ShownCpu = $shownCpu
        ShownHandles = $shownHandles
        ShownThreads = $shownThreads
        HiddenWS = $hiddenWS
        HiddenPrivate = $hiddenPriv
        HiddenCpu = $hiddenCpu
        SettingsWS = $settingsWS
        SettingsPrivate = $settingsPriv
        SettingsCpu = $settingsCpu
        AfterSettingsWS = $afterWS
        AfterSettingsPrivate = $afterPriv
        AfterSettingsCpu = $afterCpu
        Hooks = $info.Hooks
        KeyLaunchOk = $launchOk
        SettingsOk = $settingsOk
        Errors = $info.Errors
        Error = $err
    }
    $rows.Add($row)
    $rows | Export-Csv -LiteralPath $outCsv -NoTypeInformation -Encoding UTF8

    Write-Host ("cycle {0}/{1}: cold={2}ms shown={3}MB/{4}% hidden={5}MB/{6}% settings={7}MB/{8}% launch={9} settingsOk={10} hooks={11} errs={12} {13}" -f `
        $i, $Cycles, $cold, $row.ShownPrivate, $row.ShownCpu, $row.HiddenPrivate, $row.HiddenCpu, $row.SettingsPrivate, $row.SettingsCpu, $launchOk, $settingsOk, $row.Hooks, $row.Errors, $err)

    if ($appPid -ne 0) {
        Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 300
}

Write-Host "done: $outCsv"
