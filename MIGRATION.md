# SmartLauncher 迁移 / 同步

当前版本：v1.3.5（窗口总览面板）

## 目录说明

- `deploy\`：可直接运行的部署目录（exe + launcher.json + Cache + README）
- `SmartLauncherNext.bundle`：完整 git 历史，可在新电脑克隆出源码仓库
- `SmartLauncherNext-backup.zip`：源码 + 构建产物 + 配置 + 图标的一次性备份
- 本文件

## 一、在新电脑上恢复源码（二选一）

### 方案 A：用 git bundle（推荐，保留提交历史）

新电脑需要安装 Git。

```powershell
git clone SmartLauncherNext.bundle SmartLauncherNext
cd SmartLauncherNext
powershell -ExecutionPolicy Bypass -File build.ps1
```

构建只需要 Windows 自带的 .NET Framework 4.8 和 `csc.exe`，不需要 .NET SDK / NuGet / 联网。产物在 `out\`。

### 方案 B：没有 Git，直接解压备份

```powershell
Expand-Archive SmartLauncherNext-backup.zip -DestinationPath D:\Dev\SmartLauncherNext
```

解压后已有 `src`、`tests`、`docs`，根目录还有可直接运行的 `SmartLauncherNext.exe`、`launcher.json`、`Cache`；也可以执行 `build.ps1` 重新编译。

## 二、部署到新电脑

最省事的方式：保持路径完全一致。

```text
D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe
```

把 `deploy\` 整个目录复制到新电脑的同一路径，包含：

- `SmartLauncherNext.exe`
- `launcher.json`
- `launcher.json.bak`
- `Cache\Icons`
- `README.md`

如果新电脑没有 D: 盘，改到 `C:\Tools\SmartLauncher_Next_Lite`，然后同步修改：

1. Quicker 动作里的 exe 路径
2. 桌面/开始菜单的「SmartLauncher 设置」快捷方式
3. `launcher.json` 里 exe 类型的绝对路径
4. 自定义 `IconPath`（如果有绝对路径）

## 三、Quicker 侧

1. 最好用同一个 Quicker 账号同步动作；或旧机导出、新机导入。
2. 中键触发：Quicker 动作 → 鼠标中键 → 运行
   `D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe --show`
3. 如果新电脑的 Quicker 动作 ID 变了：
   - 在设置页里把 Path 改成新的动作 ID
   - 或直接把 Path 填成 Quicker 动作名称（代码支持按名称调用），跨机器更稳
4. 设置快捷方式：目标为 exe，参数 `--settings`

## 四、v1.3.x 的行为

- 面板是**窗口总览**：列出当前桌面的顶层窗口（Z 序，等同 Alt+Tab 顺序）
- 最多枚举 60 个窗口；面板最多显示 14 行，并按光标所在显示器工作区高度自适应（下限 4 行）
- 超出可视区域用滚轮或 `↑` `↓` 滚动；不显示滚动条，改为上下边缘渐变提示，滚到底/顶自动隐藏
- 切换窗口：点击该行，或 `↑` `↓` 选择后 `Enter`
- 当前前台窗口标「当前」，最小化窗口标「最小化」
- 单键 `A-Z`、`0-9`、`F1-F12` **仍然直接启动配置项**，只是不再显示在面板上
- 面板高度随窗口数量变化（`13 + 29×行数 + 26`），可见期间冻结
- 鼠标移动与键盘选中是同一个状态，不会出现两行同时高亮
- 每次唤出都会重新枚举窗口，不是缓存的旧列表
- `Esc` 或点击面板外关闭；右键：设置 / 退出

### 窗口过滤规则（v1.3.2 起）

与任务栏 / Alt+Tab 保持一致：

- 必须可见、有标题
- `WS_EX_TOOLWINDOW` 的窗口排除，除非同时带 `WS_EX_APPWINDOW`
- **有 owner 的窗口**：带 `WS_EX_APPWINDOW` 的保留（绿色版百度网盘属于这种），否则排除
- `DWM` cloaked 的窗口排除（不可见 / 其它虚拟桌面）

如果发现漏窗，看 `debug.log` 里的这一行：

```text
windows filter: kept=5 total=5 skipTool=19 skipOwned=0 skipCloaked=4 skipNoTitle=0
```

## 五、迁移后自检

```powershell
D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe --show
D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe --settings
D:\Tools\SmartLauncher_Next_Lite\SmartLauncherNext.exe --hide
```

- 中键能唤出
- 列表里能看到其它窗口，条目数跟实际打开的窗口对得上
- 点击窗口能切换过去（若底部弹「无法切换到该窗口」，多半是目标窗口以管理员权限运行）
- 单键能启动配置项
- 设置页能打开 / 保存
- 左键点面板外能关闭
- 悬停与方向键不会打架

## 六、以后怎么持续同步

### 源码

用私有 Git 仓库（GitHub/Gitee/自建）最稳：

```powershell
git remote add origin <私有仓库地址>
git push -u origin master
```

另一台：

```powershell
git pull
powershell -ExecutionPolicy Bypass -File build.ps1
```

### 不要把 `.git` 放进 OneDrive

正在使用的 `.git` 容易出现锁文件/冲突。要么用 Git remote，要么每次 `git bundle` 后通过网盘/U 盘传。

### 配置同步

`launcher.json` 和 `Cache\Icons` 是**机器相关**的，不建议直接进 Git。推荐：

- 在 OneDrive 建 `SmartLauncherProfiles\home\launcher.json`、`work\launcher.json`
- 换机时把对应 profile 复制到部署目录
- 或者保留两个 `launcher.json`，用时替换

## 七、注意

- 当前 exe 未签名，新电脑首次运行可能触发 SmartScreen：更多信息 → 仍要运行。
- 面板依赖 Windows 自带的 .NET Framework 4.8（Windows 10/11 通常已有）。
- `deploy\launcher.json` 是打包这台机器的配置；其中的 Photoshop 条目仍指向不存在的
  `D:\Program Files\Adobe Photoshop 2026\Photoshop.exe`，到新机器上记得改或删。
- 旧电脑的 `debug.log` 不需要迁移。
