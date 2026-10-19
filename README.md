# SmartLauncher Next

Windows 11 风格的快速启动面板。用 Quicker 的鼠标中键唤起，按单键启动已配置的 Quicker 动作、本地程序或网址。

## 已部署位置

- 程序：`D:\Toolo\SmartLauncher_Next_Lite\SmartLauncherNext.exe`
- 配置：同目录 `launcher.json`（保存时自动备份为 `launcher.json.bak`）
- 图标：同目录 `Cache\Icons\`

Quicker 动作保持原样即可（主路径现在已经存在）：

- 动作类型：运行程序或打开文件
- 路径：`D:\Toolo\SmartLauncher_Next_Lite\SmartLauncherNext.exe`
- 参数：`--show`
- 快捷键：鼠标中键

`D:\Program Files\SmartLauncher_Lite` 里的旧版本保持原样，没有覆盖。

## 面板

- 单键：`A-Z`、`0-9`、`F1-F12`；面板打开时按键由面板优先接管（全局键盘钩子），焦点不在面板上也能用\n- 鼠标左键点击面板外立即关闭；`Esc` 也关闭
- `↑` `↓` 选择，`Enter` 启动
- `Esc` 或点击面板外关闭
- 右键：设置 / 退出
- 面板 243×473；实心浅色/深色面 + 1px 边框 + DWM 圆角 + 轻微阴影（不再使用截屏伪亚克力）\n- 跟随系统深浅色；面板为实心磨砂面，不再截屏/模糊

## 设置

运行 `SmartLauncherNext.exe --settings`，或右键面板 → 设置。

- 分类、快捷键、名称、类型、路径 / 动作 ID、图标
- 类型：Quicker 动作（动作 ID 或名称）、本地程序、网址
- 快捷键：点一下再按一个键；`Delete` 清除
- 图标：自动提取 exe / 开始菜单图标；也可以选择 PNG / ICO / JPG，自动缓存
- 导入旧配置：从 `D:\Program Files\SmartLauncher_Lite` 导入并复制图标
- 保存：写入 `launcher.json` 并备份，面板立即刷新

## 后台与轻量

- 面板隐藏后空闲 5 分钟自动退出，后台进程归零。
- 在设置里取消“空闲 5 分钟后退出后台”可以常驻；Task Manager 约 2MB，唤出几乎即时。
- 冷启动约 0.2 秒；使用中唤出很快。
- 单文件 exe 约 96KB，依赖 Windows 自带的 .NET Framework 4.8，不需要安装运行时。
- Task Manager 的“内存”（私有工作集）约 14MB（面板打开）/ 2MB（隐藏压缩后）；“提交大小”约 100MB，是 WPF/.NET 的虚拟提交，不是物理内存。

## 命令行

- `--show`：显示面板（Quicker 用这个）
- `--toggle`：显示 / 隐藏
- `--settings`：打开设置
- `--background`：只启动后台，不显示面板
- `--light` / `--dark`：仅调试用；正常跟随系统
- `--instance=<name>`：测试用实例隔离，不影响正式配置

## 测试

- `tests\smoke.ps1`：启动 → 显示（并检查面板内容非空）→ Esc → 设置 → 关闭，失败返回 1。
- `tests\stability-test.ps1`：20 轮采样，需要先准备测试目录；脚本会把结果写入 CSV。
- `tests\stability-report.md`：v1.2 的 20 轮结果、汇总和图表；原始数据在 `tests\stability-results.csv`。

## 构建

Windows 自带 `csc.exe` 即可，不需要 .NET SDK、NuGet 或联网：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

源文件在 `src\`，输出在 `out\`。

## 常见问题

- Quicker 没运行：首次调用会先拉起 Quicker，可能慢几秒；保持 Quicker 常驻即可。
- 动作 ID 失效：Quicker 会自己提示；在设置里更新 ID，或改填动作名称。
- 首次运行 SmartScreen：exe 未签名，选择“更多信息 → 仍要运行”。
- 图标不清晰：旧缓存是 32px；在设置里重新选择高清图标，或让程序从 exe / 快捷方式提取。
- 想恢复旧版：Quicker 动作的备用路径仍然指向 `D:\Program Files\SmartLauncher_Lite\SmartLauncherNext.exe`。
