# Changelog

## 1.1 - 2026-10-19

- 面板改为不透明实心面 + WindowChrome/DWM 圆角与阴影，移除分层截屏伪亚克力
- 悬停改为原生 PreviewMouseMove；外点关闭走 Deactivated；去掉点击/悬停轮询
- 设置窗口可调整大小；每行 ▶ 试运行；筛选；上移/下移；exe 路径校验
- 设置窗口改为非模态，修复 ShowDialog 的 ReleaseCapture Win32Exception
- 增加 `--instance=` 测试实例隔离
- 配置增加 SchemaVersion；程序集版本 1.1
- 增加 smoke 测试；PanelWindow 行布局拆到 partial 文件
