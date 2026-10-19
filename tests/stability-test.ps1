param(
    [int]$Cycles = 20,
    [string]$TestDir = 'D:\Dev\Deepseek-00\.tmp\stabilitytest',
    [string]$SourceDir = 'D:\Dev\Deepseek-00\SmartLauncherNext'
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $TestDir 'SmartLauncherNext.exe'
$instance = '--instance=stability'
$log = Join-Path $TestDir 'debug.log'
$csv = Join-Path $TestDir 'stability-results.csv'

if (!(Test-Path $TestDir)) { New-Item -ItemType Directory -Path $TestDir | Out-Null }
Copy-Item (Join-Path $SourceDir 'out\SmartLauncherNext.exe') $exe -Force
if (!(Test-Path (Join-Path $TestDir 'launcher.json'))) {
    Copy-Item (Join-Path $SourceDir 'out\launcher.json') $TestDir -Force
}
if (!(Test-Path (Join-Path $TestDir 'Cache'))) {
    Copy-Item (Join-Path $SourceDir 'out\Cache') $TestDir -Recurse -Force
}

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
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  public static IntPtr FindPanel(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid) {
        StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
        if (t.ToString() == "SmartLauncherNextPanel") { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static IntPtr FindSecondary(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
        string s = t.ToString();
        if (s.StartsWith("SmartLauncher") && s != "SmartLauncherNextPanel") { found = h; return false; }
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

function Wait-Panel([int]$appPid, [int]$timeoutMs) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        $h = [StabWin]::FindPanel([uint32]$appPid)
        if ($h -ne [IntPtr]::Zero -and [StabWin]::IsWindowVisible($h)) { return $h }
        Start-Sleep -Milliseconds 25
    }
    return [IntPtr]::Zero
}
function Wait-Window([IntPtr]$handle, [int]$timeoutMs) {
    if ($handle -eq [IntPtr]::Zero) { return $false }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        if ([StabWin]::IsWindowVisible($handle)) { return $true }
        Start-Sleep -Milliseconds 25
    }
    return $false
}

function Get-PrivateWSMB {
    try {
        $s = Get-Counter '\Process(SmartLauncherNext)\Working Set - Private' -ErrorAction Stop
        $sum = 0.0
        foreach ($sample in $s.CounterSamples) { $sum += [double]$sample.CookedValue }
        return [math]::Round($sum / 1MB, 1)
    }
    catch {
        return $null
    }
}

function Get-Metrics($p, [int]$cpuMs = 1000) {
    try {
        $p.Refresh()
        $privateWS = Get-PrivateWSMB
        $commit = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        $workingSet = [math]::Round($p.WorkingSet64 / 1MB, 1)
        $handles = $p.HandleCount
        $threads = $p.Threads.Count
        $t0 = $p.TotalProcessorTime
        Start-Sleep -Milliseconds $cpuMs
        $p.Refresh()
        $t1 = $p.TotalProcessorTime
        $cpu = [math]::Round((($t1 - $t0).TotalMilliseconds / $cpuMs) * 100 / [Environment]::ProcessorCount, 2)
        return [pscustomobject]@{
            PrivateWS = $privateWS
            Commit = $commit
            WorkingSet = $workingSet
            Handles = $handles
            Threads = $threads
            Cpu = $cpu
        }
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

Get-Process -Name 'SmartLauncherNext' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Remove-Item $csv -ErrorAction SilentlyContinue

$rows = New-Object System.Collections.Generic.List[object]
$failCount = 0

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
    $escFailed = $false
    $settingsOk = $false
    $err = ''

    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        cmd.exe /c start "" "$exe" --show --pin --light $instance
        $p = Wait-App 6000
        if (-not $p) { throw 'process not started' }
        $appPid = $p.Id
        $panelH = Wait-Panel $appPid 6000
        if ($panelH -eq [IntPtr]::Zero) { throw 'panel not visible' }
        $sw.Stop()
        $cold = $sw.ElapsedMilliseconds
        $p.Refresh()
        $startupCpu = [math]::Round($p.TotalProcessorTime.TotalMilliseconds, 0)

        Start-Sleep -Milliseconds 800
        $shown = Get-Metrics $p 1000

        [StabWin]::SetForegroundWindow($panelH) | Out-Null
        Start-Sleep -Milliseconds 250
        [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
        $stillVisible = Wait-Window $panelH 3000
        if ($stillVisible) { $escFailed = $true; throw 'panel did not hide on Esc' }

        Start-Sleep -Milliseconds 16500
        $hidden = Get-Metrics $p 1000

        cmd.exe /c start "" "$exe" --settings --light $instance
        $sw2 = [Diagnostics.Stopwatch]::StartNew()
        while ($sw2.ElapsedMilliseconds -lt 8000) {
            $settingsH = [StabWin]::FindSecondary([uint32]$appPid)
            if ($settingsH -ne [IntPtr]::Zero) { break }
            Start-Sleep -Milliseconds 50
        }
        if ($settingsH -eq [IntPtr]::Zero) { throw 'settings window not found' }
        Start-Sleep -Milliseconds 600
        $settings = Get-Metrics $p 1000

        [StabWin]::PostMessage($settingsH, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        Start-Sleep -Milliseconds 500
        $settingsOk = $true
        $after = Get-Metrics $p 1000
    }
    catch {
        $err = $_.Exception.Message
    }

    if ($err.Length -gt 0) { $failCount++ }
    $info = Read-LogInfo

    $row = [pscustomobject]@{
        Cycle = $i
        ColdStartMs = $cold
        StartupCpuMs = $startupCpu
        ShownPrivateWS = if ($null -ne $shown) { $shown.PrivateWS } else { $null }
        ShownCommit = if ($null -ne $shown) { $shown.Commit } else { $null }
        ShownWorkingSet = if ($null -ne $shown) { $shown.WorkingSet } else { $null }
        ShownCpu = if ($null -ne $shown) { $shown.Cpu } else { $null }
        ShownHandles = if ($null -ne $shown) { $shown.Handles } else { $null }
        ShownThreads = if ($null -ne $shown) { $shown.Threads } else { $null }
        HiddenPrivateWS = if ($null -ne $hidden) { $hidden.PrivateWS } else { $null }
        HiddenCommit = if ($null -ne $hidden) { $hidden.Commit } else { $null }
        HiddenCpu = if ($null -ne $hidden) { $hidden.Cpu } else { $null }
        SettingsPrivateWS = if ($null -ne $settings) { $settings.PrivateWS } else { $null }
        SettingsCommit = if ($null -ne $settings) { $settings.Commit } else { $null }
        SettingsCpu = if ($null -ne $settings) { $settings.Cpu } else { $null }
        AfterSettingsPrivateWS = if ($null -ne $after) { $after.PrivateWS } else { $null }
        AfterSettingsCommit = if ($null -ne $after) { $after.Commit } else { $null }
        AfterSettingsCpu = if ($null -ne $after) { $after.Cpu } else { $null }
        Hooks = $info.Hooks
        EscHideOk = (-not $escFailed)
        SettingsOk = $settingsOk
        Errors = $info.Errors
        Error = $err
    }
    $rows.Add($row)
    $rows | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8

    Write-Host ("cycle {0}/{1}: cold={2}ms shown={3}MB commit={4}MB hidden={5}MB settings={6}MB after={7}MB cpu={8}/{9}/{10}% err={11}" -f `
        $i, $Cycles, $cold, $row.ShownPrivateWS, $row.ShownCommit, $row.HiddenPrivateWS, $row.SettingsPrivateWS, $row.AfterSettingsPrivateWS, $row.ShownCpu, $row.HiddenCpu, $row.SettingsCpu, $err)

    if ($appPid -ne 0) {
        Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 300
}

$data = Import-Csv -LiteralPath $csv
$summaryColumns = @('ColdStartMs','StartupCpuMs','ShownPrivateWS','ShownCommit','ShownCpu','HiddenPrivateWS','HiddenCpu','SettingsPrivateWS','SettingsCpu','AfterSettingsPrivateWS','AfterSettingsCpu','ShownHandles','ShownThreads')
Write-Host '--- summary ---'
foreach ($col in $summaryColumns) {
    $vals = @($data.$col | Where-Object { $_ -ne '' -and $null -ne $_ } | ForEach-Object { [double]$_ })
    if ($vals.Count -eq 0) { continue }
    $avg = [math]::Round(($vals | Measure-Object -Average).Average, 2)
    $min = [math]::Round(($vals | Measure-Object -Minimum).Minimum, 2)
    $max = [math]::Round(($vals | Measure-Object -Maximum).Maximum, 2)
    Write-Host ("{0}: avg={1} min={2} max={3}" -f $col, $avg, $min, $max)
}
$errCount = ($data | Where-Object { $_.Error -ne '' -or $_.Errors -ne '0' }).Count
Write-Host ("completed={0} failures={1} errorRows={2} csv={3}" -f $data.Count, $failCount, $errCount, $csv)
if ($failCount -gt 0) { exit 1 }
