# Changelog

## 1.3.4 - 2026-10-23

- 修复上下渐隐块的直角破坏面板圆角：渐隐 Border 按内圆角 9px 裁切
- 每次唤出都把列表滚动位置重置到顶部，不再继承上次的位置

## 1.3.3 - 2026-10-23

- 面板最多显示 14 行，并按光标所在显示器工作区高度自适应（下限 4 行）
- 枚举上限提到 60：超出可视区域的行进入滚动区，不再丢弃
- 隐藏滚动条，滚轮 + ↑↓ 滚动（↑↓ 仍走 BringIntoView）
- 上下边缘渐变提示可滚动，滚动到顶/底时自动隐藏；颜色取面板底色，切主题时重建
- 图标按序号降级：第 20 行之后或构建超 250ms 时直接用进程图标，唤出不被卡死窗口拖慢
- 移除「还有 N 个窗口」文字（现在都能滚到）

## 1.3.2 - 2026-10-22

- 修复漏窗：窗口过滤改为与任务栏/Alt+Tab 一致的规则（有 owner 但带 WS_EX_APPWINDOW 的窗口保留，例如绿色版百度网盘）
- 新增窗口过滤诊断日志（skipTool / skipOwned / skipCloaked / skipNoTitle）

## 1.3.1 - 2026-10-22

- 鼠标悬停与键盘选中合并为同一个选中态：移动鼠标即移动高亮，方向键选中后鼠标不动则保持

## 1.3.0 - 2026-10-22

- 面板改为窗口总览：列出当前桌面的顶层窗口（Z 序，等同 Alt+Tab 顺序），点击 / `Enter` 切换
- 窗口过滤沿用 PowerToys WindowWalker 的谓词：可见、无 owner、非工具窗口、非 cloaked、非本进程
- 面板高度按窗口数量动态计算（最多 8 行），可见期间冻结尺寸
- 按键分发与视觉行解耦：启动项改为「键 → 条目」字典，行不再渲染仍可单键启动
- 当前前台窗口标记「当前」，最小化窗口标记「最小化」
- 激活顺序：最小化先 `ShowWindow(SW_RESTORE)`，失败退 `SC_RESTORE`，再 `SetForegroundWindow`

## 1.2.4 - 2026-10-22

- A：移除 6 秒自动隐藏；外点关闭保留 Deactivated，并加面板内 30ms 左键轮询兜底
- A：焦点激活不再依赖 Alt；补充面板 Focusable/键盘焦点和激活日志
- B：按光标所在显示器的 DPI 与工作区定位，使用 SetWindowPos 物理坐标
- C：设置窗口增加 10px 圆角区域
- D：图标缓存加入文件修改时间，增加 ExtractAssociatedIcon 兜底
- D：设置页新增“导出配置 / 导入配置”

## 1.2.3 - 2026-10-20

- 新增 `--hide`：只隐藏现有面板，不退出进程
- 修复 `--toggle`：之前实际等同 `--show`
- 供 Quicker watcher 做“左键外点关闭”POC

## 1.2.2 - 2026-10-20

- 移除 Alt 模拟抢焦点（会在部分时序下把 Alt 留在按下状态，导致 Alt+Tab、任务栏、开始菜单、按键全部失效）
- 改为 SetForegroundWindow + Quicker 动作里的“激活窗口”步骤
- 启动时只发送 Alt 抬起作为清理，不再发送 Alt 按下
- 任务栏/开始菜单不会再被卡住

## 1.2.1 - 2026-10-19

- 面板壳退回 layered WPF 合成（实心面 + 阴影），修复 DWM 黑块/空白
- 恢复左键外点关闭，鼠标移出不再自动关闭
- 保留 v1.2 的设置页、测试、配置版本
- 面板圆角 7→10px、菜单 6→8px，接近 Win11 应用圆角
- 移除 overlay/auto-hide/WindowChrome/SetWindowRgn/Redraw 这一套

## 1.2 - 2026-10-19

- 悬停增加 35ms 光标兜底，修复蓝色高亮偶发卡住
- 面板可见性改为 Hidden/Showing/Visible/Hiding 状态机
- SettingsWindow 布局拆到 partial 文件
- 设置页左侧 ≡ 拖拽排序
- 修复右键菜单四个黑角（Popup 改为透明）
- 面板显示/隐藏改为即时，去掉可能卡住的渲染动画
- 修复移除动画后面板内容空白（ShowPanel 残留 Opacity=0）
- 移除面板 WindowChrome，改用 SetWindowRgn 圆角 + RedrawWindow/DwmFlush，修复黑色色块残留
- 30 次显示/隐藏压力无黑块；面板不再有阴影
- 外点关闭改为鼠标移出面板 500ms 自动关闭，修复面板未激活时外点不生效/黑块残留
- 移除不可靠的全局鼠标钩子/点击轮询/鼠标捕获
- 设置关闭后触发工作集压缩
- 20 轮稳定性压测：20/20 通过，结果见 `tests/stability-report.md`

## 1.1 - 2026-10-19

- 面板改为不透明实心面 + WindowChrome/DWM 圆角与阴影，移除分层截屏伪亚克力
- 悬停改为原生 PreviewMouseMove；外点关闭走 Deactivated；去掉点击/悬停轮询
- 设置窗口可调整大小；每行 ▶ 试运行；筛选；上移/下移；exe 路径校验
- 设置窗口改为非模态，修复 ShowDialog 的 ReleaseCapture Win32Exception
- 增加 `--instance=` 测试实例隔离
- 配置增加 SchemaVersion；程序集版本 1.1
- 增加 smoke 测试；PanelWindow 行布局拆到 partial 文件
