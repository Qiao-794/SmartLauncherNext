# Changelog

## 1.2 - 2026-10-19

- 悬停增加 35ms 光标兜底，修复蓝色高亮偶发卡住
- 面板可见性改为 Hidden/Showing/Visible/Hiding 状态机
- SettingsWindow 布局拆到 partial 文件
- 设置页左侧 ≡ 拖拽排序
- 修复右键菜单四个黑角（Popup 改为透明）
- 面板显示/隐藏改为即时，去掉可能卡住的渲染动画
- 修复移除动画后面板内容空白（ShowPanel 残留 Opacity=0）
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
