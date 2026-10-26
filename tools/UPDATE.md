# SmartLauncher updater

`update.ps1` checks `latest.json`, downloads the release only when needed, verifies SHA256, stops the app, replaces the exe/README/version, and keeps `launcher.json` and `Cache` untouched.

## Existing install

The installer copies `update.ps1` into:

```text
D:\Tools\SmartLauncher_Next_Lite\update.ps1
```

Quicker can run:

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Tools\SmartLauncher_Next_Lite\update.ps1" -Show
```

- `-Show` launches the panel after checking/updating.
- Without `-Show`, it only installs and exits.

## First install

If `D:\Tools\SmartLauncher_Next_Lite\update.ps1` does not exist yet, download the script first.

Quicker steps:

1. Download:
   ```text
   https://raw.githubusercontent.com/Qiao-794/SmartLauncherNext/main/tools/update.ps1
   ```
   to:
   ```text
   %TEMP%\SmartLauncherUpdate.ps1
   ```
2. Run:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%TEMP%\SmartLauncherUpdate.ps1" -Show
   ```

The script installs to `D:\Tools\SmartLauncher_Next_Lite` and copies itself there, so future runs can use the installed copy.

## Options

```powershell
# Check only, do not download
.\update.ps1 -CheckOnly

# Force reinstall the current latest version
.\update.ps1 -Force -NoLaunch

# Check/update and open the panel
.\update.ps1 -Show

# Custom install location
.\update.ps1 -InstallDir "D:\Tools\SmartLauncher_Next_Lite" -Show

# Restore the previous exe (SmartLauncherNext.exe.bak)
.\update.ps1 -Rollback
```

## Behaviour details

- Only the instance whose path equals `<InstallDir>\SmartLauncherNext.exe` is stopped;
  other builds or copies are left alone.
- Every run appends to `<InstallDir>\update.log` (rotated at 256 KB), so a silent
  failure from Quicker can still be diagnosed afterwards.
- `version.txt` is cross-checked against the exe's product version; when they differ
  the exe wins and the file is repaired, with a warning in the log.
- Two concurrent runs are guarded by a named mutex (the second one exits with code 3).
- After copying, the installed exe version is verified again; on mismatch the
  `.bak` file is restored automatically.
- The updater never touches `launcher.json`, `launcher.json.bak` or `Cache`.

## What it preserves

Never overwritten:

- `launcher.json`
- `launcher.json.bak`
- `Cache\Icons`

Replaced:

- `SmartLauncherNext.exe`
- `README.md`
- `version.txt`

A backup of the previous exe is written as `SmartLauncherNext.exe.bak`.

## Exit codes

- `0`: up to date, check completed, rollback done, or update succeeded
- `1`: unexpected error
- `2`: update source unreachable / `latest.json` invalid
- `3`: another update is already running
- `4`: download, size, SHA256, extraction or version check failed
- `5`: post-install verification failed (previous exe restored)
- `6`: `-Rollback` requested but no `.bak` exists

## Security

- Uses only the public `latest.json` and release zip.
- No GitHub token is needed for updating.
- Verifies size and SHA256 before replacing files.
- Does not run arbitrary commands from `latest.json`.
