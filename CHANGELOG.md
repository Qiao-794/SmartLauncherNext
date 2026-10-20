# Changelog

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
