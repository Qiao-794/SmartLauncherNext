# SmartLauncher Next

Windows 11 风格的窗口总览面板。用 Quicker 的鼠标中键唤起，上方列出当前桌面的窗口，点击或回车切换；同时保留单键启动 Quicker 动作 / 本地程序 / 网址。

## 已部署位置

- 程序：`D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe`
- 配置：同目录 `launcher.json`（保存时自动备份为 `launcher.json.bak`）
- 图标：同目录 `Cache\Icons\`

Quicker 动作保持原样即可（主路径现在已经存在）：

- 动作类型：运行程序或打开文件
- 路径：`D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe`
- 参数：`--show`
- 快捷键：鼠标中键

`D:\Program Files\SmartLauncher_Lite` 里的旧版本保持原样，没有覆盖。

## 面板

- 窗口列表：枚举当前桌面的顶层窗口（Z 序，等同 Alt+Tab 顺序），最多枚举 60 个
- 显示行数：最多 14 行，并按光标所在显示器工作区高度自适应（下限 4 行）；超出部分滚动查看
- 滚动：不显示滚动条，滚轮滚动 + `↑` `↓`（选中行会自动滚入视野）；滚动到顶/底时上下边缘渐变提示自动隐藏
- 窗口行：图标 + 标题 + 进程名；当前前台窗口标「当前」，最小化标「最小化」
- 切换：点击窗口行，或 `↑` `↓` 选择后 `Enter`
- 单键：`A-Z`、`0-9`、`F1-F12` 仍然直接启动配置项（列表不显示键位，按键由全局钩子接管）
- `Esc` 或点击面板外关闭；右键：设置 / 退出
- 面板高度随显示行数动态变化（13 + 29×行数 + 26），可见期间冻结尺寸
- 宽 243、窗口 269（13px 阴影边距）；layered WPF 合成、实心面 + 1px 边框 + 10px 圆角 + DropShadow 阴影；不截屏模糊，不使用 DWM/WindowChrome
- 鼠标移出不会自动关闭；左键点击面板外关闭（Deactivated + 30ms 左键轮询兜底）
- 跟随系统深浅色；面板为实心磨砂面，不再截屏/模糊

## 设置

运行 `SmartLauncherNext.exe --settings`，或右键面板 → 设置。

- 分类、快捷键、名称、类型、路径 / 动作 ID、图标
- 类型：Quicker 动作（动作 ID 或名称）、本地程序、网址
- 快捷键：点一下再按一个键；`Delete` 清除
- 图标：自动提取 exe / 开始菜单图标；也可以选择 PNG / ICO / JPG，自动缓存
- 导入旧配置：从 `D:\Program Files\SmartLauncher_Lite` 导入并复制图标
- 导出配置 / 导入配置：单独备份或恢复 launcher.json（图标目录需一起复制）
- 保存：写入 `launcher.json` 并备份，面板立即刷新

## 后台与轻量

- 面板隐藏后空闲 5 分钟自动退出，后台进程归零。
- 在设置里取消“空闲 5 分钟后退出后台”可以常驻；Task Manager 约 2MB，唤出几乎即时。
- 冷启动约 0.2 秒；使用中唤出很快。
- 单文件 exe 约 96KB，依赖 Windows 自带的 .NET Framework 4.8，不需要安装运行时。
- Task Manager 的“内存”（私有工作集）约 14MB（面板打开）/ 2MB（隐藏压缩后）；“提交大小”约 100MB，是 WPF/.NET 的虚拟提交，不是物理内存。

## 命令行

- `--show`：显示面板（Quicker 用这个）
- Quicker 中键动作保持原来的 `--show` 即可。只有出现“面板可见但单键无响应/外点不关”时，才在动作里加“窗口操作 → 激活窗口”作为兜底
- `--hide`：隐藏现有面板（不退出进程）
- `--toggle`：显示 / 隐藏
- `--settings`：打开设置
- `--background`：只启动后台，不显示面板
- `--light` / `--dark`：仅调试用；正常跟随系统
- `--instance=<name>`：测试用实例隔离，不影响正式配置

## 测试

```powershell
powershell -ExecutionPolicy Bypass -File tests\smoke.ps1
```

覆盖：面板显示、键盘钩子、窗口行数/可见行数/高度公式、滚轮滚到底、重新打开回到顶部、`Esc` 关闭、设置窗口开关、`debug.log` 无异常。默认会临时开 16 个控制台窗口保证列表可滚动，可用 `-SpawnWindows 0` 跳过；测试产物在 `.smoke\`（已加入 .gitignore）。

- `tests\stability-test.ps1`：20 轮采样，需要先准备测试目录；脚本会把结果写入 CSV。
- `tests\stability-report.md`：v1.2 的 20 轮结果、汇总和图表；原始数据在 `tests\stability-results.csv`。

## 构建

Windows 自带 `csc.exe` 即可，不需要 .NET SDK、NuGet 或联网：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

源文件在 `src\`，输出在 `out\`。

## 打包

```powershell
powershell -ExecutionPolicy Bypass -File package.ps1
```

一条命令完成：构建 → 生成 git bundle → 组装 `deploy\` → 打源码备份 → 汇总为 `SmartLauncherNext-migration-<version>.zip`，最后打印 exe 与 zip 的 SHA256。

常用参数：`-Version 1.3.4`（默认读 `src\VersionInfo.cs`）、`-OutputDir`、`-DeploySource`（取 `launcher.json` / `Cache` 的目录）、`-SkipBuild`、`-KeepStaging`。

## 常见问题

- Quicker 没运行：首次调用会先拉起 Quicker，可能慢几秒；保持 Quicker 常驻即可。
- 动作 ID 失效：Quicker 会自己提示；在设置里更新 ID，或改填动作名称。
- 首次运行 SmartScreen：exe 未签名，选择“更多信息 → 仍要运行”。
- 图标不清晰：旧缓存是 32px；在设置里重新选择高清图标，或让程序从 exe / 快捷方式提取。
- 想恢复旧版：Quicker 动作的备用路径仍然指向 `D:\Program Files\SmartLauncher_Lite\SmartLauncherNext.exe`。
