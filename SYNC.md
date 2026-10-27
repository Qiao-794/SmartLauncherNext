# Cross-PC sync / AI handoff

## Repository

```text
https://github.com/Qiao-794/SmartLauncherNext
```

- Branch: `main`
- Source version: `1.4.2`
- Build: Windows `.NET Framework 4.8` + `build.ps1`
- Deployment: `D:\Tools\SmartLauncher_Next_Lite`
- Release: `v1.4.2`
- Latest JSON: `https://raw.githubusercontent.com/Qiao-794/SmartLauncherNext/main/latest.json`

## Prompt to paste into the AI assistant on the other PC

```text
请在这台电脑上同步 SmartLauncherNext：

仓库：https://github.com/Qiao-794/SmartLauncherNext
分支：main
源码目录：D:\Dev\Deepseek-00\SmartLauncherNext
部署目录：D:\Tools\SmartLauncher_Next_Lite

要求：
1. 如果本地没有源码仓库，git clone 到源码目录。
2. 如果已有仓库，git pull --ff-only。
3. 确认 src\VersionInfo.cs 是 1.4.2。
4. 运行 build.ps1。
5. 部署时只覆盖：
   - SmartLauncherNext.exe
   - README.md
   - version.txt
   不要覆盖 launcher.json、launcher.json.bak、Cache。
6. 运行 tests\smoke.ps1；再运行 --show / --hide 验证。
7. 报告当前 git commit、exe 版本、测试结果和所有失败项。
```

## First clone

```powershell
git clone https://github.com/Qiao-794/SmartLauncherNext.git D:\Dev\Deepseek-00\SmartLauncherNext
cd D:\Dev\Deepseek-00\SmartLauncherNext
powershell -ExecutionPolicy Bypass -File build.ps1
```

## Update an existing clone

```powershell
cd D:\Dev\Deepseek-00\SmartLauncherNext
git status
git pull --ff-only
powershell -ExecutionPolicy Bypass -File build.ps1
```

If `git pull --ff-only` fails, do not force. Inspect:

```powershell
git log --oneline --graph --decorate --all -20
git status
```

## Deploy

Copy only:

```text
out\SmartLauncherNext.exe
README.md
version.txt
```

to:

```text
D:\Tools\SmartLauncher_Next_Lite
```

Never overwrite:

```text
launcher.json
launcher.json.bak
Cache\Icons
```

The app and Quicker both use `D:\Tools\SmartLauncher_Next_Lite`.

## Verify

```powershell
SmartLauncherNext.exe --show
SmartLauncherNext.exe --hide
powershell -ExecutionPolicy Bypass -File tests\smoke.ps1
```

`smoke.ps1` may occasionally fail Esc once because of focus timing; rerun before treating it as a real failure.

## Push changes from the other PC

Reading/cloning the public repo needs no login. Pushing needs GitHub authentication.

Do not put a token in the repository or in a Quicker action. Use Git Credential Manager or an environment variable locally.

## What is not in Git

- `launcher.json` (machine-specific)
- `launcher.json.bak`
- `Cache\Icons`
- `out\`
- `debug.log`

Copy config/icons separately if the other PC needs the same entries. `D:\Tools\SmartLauncher_Next_Lite` is the canonical deploy path.
