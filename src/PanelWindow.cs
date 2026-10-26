using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Bitmap = System.Drawing.Bitmap;
using Graphics = System.Drawing.Graphics;
using CopyPixelOperation = System.Drawing.CopyPixelOperation;

namespace SmartLauncher
{
    public partial class PanelWindow : Window
    {
        private const double ShadowMargin = 13;
        private const double PanelWidth = 243;

        private AppConfig _config;
        private bool _pinned;
        private readonly StackPanel _list;
        private readonly ScrollViewer _scroll;
        private readonly Border _topFade;
        private readonly Border _bottomFade;
        private readonly Border _panel;
        private readonly Grid _content;
        private readonly Border _status;
        private readonly TextBlock _statusText;
        private readonly List<RowView> _rows = new List<RowView>();
        private readonly Dictionary<string, Item> _keyMap = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _statusTimer;
        private readonly DispatcherTimer _trimTimer;
        private int _selected = -1;
        private DateTime _shownAt;
        private enum PanelState { Hidden, Visible, Hiding }
        private PanelState _state = PanelState.Hidden;

        private bool IsHiding
        {
            get { return _state == PanelState.Hiding; }
        }
        private bool _suppressDeactivate;
        private bool _settingsOpen;
        private int _hideGen;
        private readonly DispatcherTimer _focusTimer;
        private readonly DispatcherTimer _clickTimer;
        private bool _leftWasDown;

        private readonly DispatcherTimer _activationTimer;
        private bool _everActive;
        private int _focusAttempts;
        private DateTime _activatedAt;
        private int _cursorX;
        private int _cursorY;
        private bool _hasCursorPoint;
        private readonly DispatcherTimer _hoverTimer;
        private DateTime _focusGraceUntil;

        public event EventHandler Activity;

        public bool SettingsOpen
        {
            get { return _settingsOpen; }
        }

        public PanelWindow(AppConfig config, bool pinned)
        {
            Log.Write("Panel ctor start pinned=" + pinned);
            _config = config;
            _pinned = pinned;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = true;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = PanelWidth + ShadowMargin * 2;
            Height = 473 + ShadowMargin * 2;
            SizeToContent = SizeToContent.Manual;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "SmartLauncherNextPanel";
            Ui.ApplyAppIcon(this);
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            FontFamily = Theme.UiFont;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            Grid root = new Grid();
            Content = root;

            Border shadowBack = new Border();
            shadowBack.Margin = new Thickness(ShadowMargin);
            shadowBack.CornerRadius = new CornerRadius(10);
            shadowBack.Background = Theme.PanelSolidBrush;
            shadowBack.IsHitTestVisible = false;
            DropShadowEffect panelShadow = new DropShadowEffect();
            panelShadow.BlurRadius = 11;
            panelShadow.ShadowDepth = 2;
            panelShadow.Direction = 270;
            panelShadow.Opacity = 0.26;
            panelShadow.Color = Colors.Black;
            shadowBack.Effect = panelShadow;
            root.Children.Add(shadowBack);
            _panel = new Border();
            _panel.Margin = new Thickness(ShadowMargin);
            _panel.CornerRadius = new CornerRadius(10);
            _panel.BorderThickness = new Thickness(1);
            _panel.BorderBrush = Theme.PanelBorderBrush;
            _panel.Background = Theme.PanelSolidBrush;
            _panel.SnapsToDevicePixels = true;
            _panel.Focusable = true;

            root.Children.Add(_panel);

            _content = new Grid();
            _content.RenderTransformOrigin = new Point(0.5, 0.5);
            _panel.Child = _content;

            // 不显示滚动条，但仍可滚（滚轮 + 方向键）
            _scroll = new ScrollViewer();
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.Focusable = false;
            _scroll.Padding = new Thickness(0);
            _scroll.Background = Brushes.Transparent;
            _scroll.ScrollChanged += OnScrollChanged;
            _content.Children.Add(_scroll);

            _list = new StackPanel();
            _list.Margin = new Thickness(4.5, 5.5, 4.5, 5.5);
            _scroll.Content = _list;

            // 上下边缘渐隐：提示还能滚，但不占宽度
            _topFade = MakeFade(true);
            _content.Children.Add(_topFade);
            _bottomFade = MakeFade(false);
            _content.Children.Add(_bottomFade);
            UpdateFadeBrushes();

            _statusText = new TextBlock();
            _statusText.FontSize = 10.5;
            _statusText.Foreground = Brushes.White;
            _statusText.TextWrapping = TextWrapping.Wrap;
            _status = new Border();
            _status.Background = Theme.StatusBgBrush;
            _status.CornerRadius = new CornerRadius(5);
            _status.Padding = new Thickness(9, 5, 9, 5);
            _status.Margin = new Thickness(7, 0, 7, 7);
            _status.HorizontalAlignment = HorizontalAlignment.Stretch;
            _status.VerticalAlignment = VerticalAlignment.Bottom;
            _status.Visibility = Visibility.Collapsed;
            _status.IsHitTestVisible = false;
            _status.Child = _statusText;
            _content.Children.Add(_status);

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(3.5);
            _statusTimer.Tick += delegate
            {
                _statusTimer.Stop();
                _status.Visibility = Visibility.Collapsed;
            };

            _trimTimer = new DispatcherTimer();
            _trimTimer.Interval = TimeSpan.FromMilliseconds(15000);
            _trimTimer.Tick += delegate
            {
                _trimTimer.Stop();
                if (!IsVisible) Native.TrimWorkingSet();
            };

            _hoverTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(35);
            _hoverTimer.Tick += delegate { UpdateHoverFromCursor(); };


            _focusTimer = new DispatcherTimer();
            _focusTimer.Interval = TimeSpan.FromMilliseconds(250);
            _focusTimer.Tick += delegate { OnFocusRetry(); };

            _clickTimer = new DispatcherTimer(DispatcherPriority.Input);
            _clickTimer.Interval = TimeSpan.FromMilliseconds(30);
            _clickTimer.Tick += delegate { WatchOutsideClick(); };







            _activationTimer = new DispatcherTimer();
            _activationTimer.Interval = TimeSpan.FromMilliseconds(350);
            _activationTimer.Tick += delegate { OnActivationTimer(); };

            Activated += delegate
            {
                _everActive = true;
                _activatedAt = DateTime.Now;
                _focusTimer.Stop();
                _activationTimer.Stop();

            };

            BuildKeyMap();
            Log.Write("Panel ctor done");

            SourceInitialized += OnSourceInitialized;
            Deactivated += OnDeactivated;
            PreviewKeyDown += OnKeyDown;
            MouseRightButtonUp += OnRightClick;
            PreviewMouseMove += OnPanelMouseMove;
            MouseLeave += OnPanelMouseLeave;

            new WindowInteropHelper(this).EnsureHandle();
            Log.Write("Panel ctor done");
        }

        public void Reload(AppConfig config)
        {
            _config = config;
            BuildKeyMap();
            BuildWindowRows();
            if (IsVisible)
            {
                UpdateLayout();
                PositionNearCursor();
                UpdateFadeState();
            }
        }

        public void ShowPanel()
        {
            if (_settingsOpen) return;
            _hideGen++;
            if (IsHiding)
            {
                SetState(PanelState.Visible);
                _content.BeginAnimation(UIElement.OpacityProperty, null);
                _content.Opacity = 1;
            }

            if (IsVisible)
            {
                _shownAt = DateTime.Now;
                InstallHooks();
                _hoverTimer.Stop();
                _hoverTimer.Start();
            StartClickWatch();
                SetState(PanelState.Visible);
                if (!IsActive) ScheduleActivation();
                return;
            }

            Theme.Load();
            BuildWindowRows();
            _panel.Background = Theme.PanelSolidBrush;
            _panel.BorderBrush = Theme.PanelBorderBrush;
            Background = Brushes.Transparent;
            _content.Opacity = 1;
            _content.RenderTransform = new TranslateTransform(0, 0);
            PositionNearCursor();
            _shownAt = DateTime.Now;
            BeginFocusGrace();
            Log.Write("ShowPanel before Show left=" + Left + " top=" + Top + " h=" + Height);
            Show();
            SetState(PanelState.Visible);
            Log.Write("ShowPanel after Show wpfVisible=" + IsVisible);
            UpdateLayout();
            // 每次唤出都从顶部开始，不继承上次的滚动位置
            if (_scroll != null)
            {
                _scroll.ScrollToTop();
                _scroll.UpdateLayout();
                Log.Write("scroll reset -> " + _scroll.VerticalOffset.ToString("0"));
            }
            UpdateFadeState();
            InstallHooks();
            _hoverTimer.Stop();
            _hoverTimer.Start();
            StartClickWatch();
            Topmost = true;
            _status.Visibility = Visibility.Collapsed;

            if (Activity != null) Activity(this, EventArgs.Empty);
        }
        public void HideNow()
        {
            HidePanel(true);
        }

        private void StartClickWatch()
        {
            _leftWasDown = false;
            _clickTimer.Stop();
            _clickTimer.Start();
        }

        private void WatchOutsideClick()
        {
            if (!IsVisible || _pinned || _settingsOpen || _suppressDeactivate)
            {
                _leftWasDown = false;
                return;
            }

            bool down = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0;
            if (!down)
            {
                _leftWasDown = false;
                return;
            }
            if (_leftWasDown) return;
            _leftWasDown = true;

            Native.POINT pt;
            if (!Native.GetCursorPos(out pt)) return;
            if (IsScreenPointInsidePanel(pt)) return;

            Log.Write("outside click poll x=" + pt.X + " y=" + pt.Y);
            HidePanel(false);
        }

        private bool IsScreenPointInsidePanel(Native.POINT pt)
        {
            try
            {
                Point tl = PointToScreen(new Point(ShadowMargin, ShadowMargin));
                Point br = PointToScreen(new Point(ActualWidth - ShadowMargin, ActualHeight - ShadowMargin));
                return pt.X >= tl.X && pt.X <= br.X && pt.Y >= tl.Y && pt.Y <= br.Y;
            }
            catch
            {
                return true;
            }
        }
        private void ActivatePanel()
        {
            Activate();
            Focus();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Native.ForceForeground(hwnd);
            _panel.Focus();
            Keyboard.Focus(_panel);
            Log.Write("ActivatePanel active=" + IsActive);
        }

        private void HidePanel(bool immediate)
        {
            if (!IsVisible || IsHiding) return;
            Hooks.Uninstall();
            _hoverTimer.Stop();
            _focusTimer.Stop();
            _activationTimer.Stop();
            _clickTimer.Stop();
            _leftWasDown = false;
            SetState(PanelState.Hiding);
            _hideGen++;
            Log.Write("HidePanel immediate=" + immediate + " pinned=" + _pinned);

            Hide();

            ResetVisuals();
            SetState(PanelState.Hidden);
            ScheduleTrim();
        }
        private void SetState(PanelState state)
        {
            if (_state == state) return;
            Log.Write("panel state " + _state + " -> " + state);
            _state = state;
        }
        private void ResetVisuals()
        {
            _content.BeginAnimation(UIElement.OpacityProperty, null);
            _content.Opacity = 1;
            _content.RenderTransform = new TranslateTransform(0, 0);
        }

        private void ScheduleTrim()
        {
            _trimTimer.Stop();
            _trimTimer.Start();
        }

        private void PositionNearCursor()
        {
            Native.POINT pt;
            Native.GetCursorPos(out pt);
            IntPtr mon = Native.MonitorFromPoint(pt, 2);
            Native.MONITORINFO mi = new Native.MONITORINFO();
            mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MONITORINFO));
            System.Drawing.Rectangle work;
            if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref mi))
                work = System.Drawing.Rectangle.FromLTRB(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right, mi.rcWork.Bottom);
            else
                work = Native.GetWorkAreaPixels();

            double scale = 1.0;
            try
            {
                uint dpiX, dpiY;
                if (mon != IntPtr.Zero && Native.GetDpiForMonitor(mon, Native.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0 && dpiX > 0)
                    scale = dpiX / 96.0;
                else
                {
                    IntPtr hwndDpi = new WindowInteropHelper(this).Handle;
                    uint dpi = hwndDpi != IntPtr.Zero ? Native.GetDpiForWindow(hwndDpi) : 96;
                    if (dpi > 0) scale = dpi / 96.0;
                }
            }
            catch
            {
                IntPtr hwndDpi = new WindowInteropHelper(this).Handle;
                uint dpi = hwndDpi != IntPtr.Zero ? Native.GetDpiForWindow(hwndDpi) : 96;
                if (dpi > 0) scale = dpi / 96.0;
            }

            double contentW = PanelWidth;
            double contentH = Height - ShadowMargin * 2;
            if (contentH <= 0) contentH = 473;

            double wpx = contentW * scale;
            double hpx = contentH * scale;
            double margin = 8 * scale;
            double x = pt.X - 24 * scale;
            double y = pt.Y + 14 * scale;

            if (x + wpx > work.Right - margin) x = work.Right - wpx - margin;
            if (x < work.Left + margin) x = work.Left + margin;
            if (y + hpx > work.Bottom - margin)
            {
                y = pt.Y - hpx - 16 * scale;
                if (y < work.Top + margin) y = work.Bottom - hpx - margin;
            }
            if (y < work.Top + margin) y = work.Top + margin;

            Left = x / scale - ShadowMargin;
            Top = y / scale - ShadowMargin;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int leftPx = (int)Math.Round(x - ShadowMargin * scale);
                int topPx = (int)Math.Round(y - ShadowMargin * scale);
                Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, leftPx, topPx, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
            Log.Write("position cursor=" + pt.X + "," + pt.Y + " work=" + work + " scale=" + scale.ToString("0.##") + " left=" + Left + " top=" + Top);
        }
        private void BuildKeyMap()
        {
            _keyMap.Clear();
            if (_config == null || _config.Categories == null) return;
            foreach (Category cat in _config.Categories)
            {
                if (cat == null || cat.Items == null) continue;
                foreach (Item item in cat.Items)
                {
                    if (item == null || string.IsNullOrEmpty(item.Key)) continue;
                    string key = item.Key.Trim().ToUpperInvariant();
                    if (key.Length == 0 || _keyMap.ContainsKey(key)) continue;
                    _keyMap[key] = item;
                }
            }
            Log.Write("keymap keys=" + _keyMap.Count);
        }

        private void LaunchItem(Item item)
        {
            if (item == null) return;
            string error;
            if (Launcher.TryRun(item, out error))
            {
                HidePanel(false);
            }
            else
            {
                ShowStatus(error);
            }
        }

        private bool TryToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            Item item;
            if (!_keyMap.TryGetValue(token, out item)) return false;
            LaunchItem(item);
            return true;
        }

        private void ShowStatus(string text)
        {
            _statusText.Text = text;
            _status.Visibility = Visibility.Visible;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void MoveSelection(int step)
        {
            if (_rows.Count == 0) return;
            int next;
            if (_selected < 0)
                next = step > 0 ? 0 : _rows.Count - 1;
            else
                next = (_selected + step + _rows.Count) % _rows.Count;
            _selected = next;
            UpdateSelection();
            _rows[next].Container.BringIntoView();
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool sel = i == _selected;
                if (_rows[i].Selected != sel)
                {
                    _rows[i].Selected = sel;
                    _rows[i].Apply();
                }
            }
        }

        private static string KeyToToken(Key key)
        {
            if (key >= Key.A && key <= Key.Z) return key.ToString();
            int d = (int)key - (int)Key.D0;
            if (d >= 0 && d <= 9) return d.ToString();
            int n = (int)key - (int)Key.NumPad0;
            if (n >= 0 && n <= 9) return n.ToString();
            int f = (int)key - (int)Key.F1;
            if (f >= 0 && f <= 11) return "F" + (f + 1);
            return null;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HidePanel(false);
                e.Handled = true;
                return;
            }

            ModifierKeys mods = Keyboard.Modifiers;
            if ((mods & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != ModifierKeys.None)
            {
                if (mods == ModifierKeys.Control && e.Key == Key.OemComma)
                {
                    OpenSettings();
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Down)
            {
                MoveSelection(1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                MoveSelection(-1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                if (_selected >= 0 && _selected < _rows.Count) ActivateWindow(_rows[_selected]);
                e.Handled = true;
                return;
            }

            string token = KeyToToken(e.Key);
            if (token == null) return;
            if (TryToken(token)) e.Handled = true;
        }

        private void BeginFocusGrace()
        {
            _everActive = false;
            _focusAttempts = 0;
            _focusGraceUntil = DateTime.Now.AddMilliseconds(2500);
            _focusTimer.Stop();
            _activationTimer.Stop();
            _activationTimer.Start();


        }

        private void ScheduleActivation()
        {
            _focusAttempts = 0;
            _focusGraceUntil = DateTime.Now.AddMilliseconds(2500);
            _focusTimer.Stop();
            _activationTimer.Stop();
            _activationTimer.Start();


        }

        private void OnActivationTimer()
        {
            _activationTimer.Stop();
            if (IsActive)
            {
                _everActive = true;

                return;
            }

            ActivatePanel();
            if (!IsActive)
            {
                _focusAttempts = 0;
                _focusTimer.Stop();
                _focusTimer.Start();
            }
        }

        private void OnFocusRetry()
        {
            _focusTimer.Stop();
            if (IsActive)
            {


                return;
            }

            _focusAttempts++;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Native.ForceForeground(hwnd);
            if (IsActive)
            {


                return;
            }
            if (_focusAttempts < 8) _focusTimer.Start();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            Log.Write("Deactivated pinned=" + _pinned + " elapsed=" + (DateTime.Now - _shownAt).TotalMilliseconds + " everActive=" + _everActive);
            if (_pinned || _suppressDeactivate || _settingsOpen) return;
            IntPtr fg = Native.GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                uint fgPid;
                Native.GetWindowThreadProcessId(fg, out fgPid);
                if (fgPid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return;
            }
            if (_config != null && !_config.HideWhenInactive) return;
            if ((DateTime.Now - _shownAt).TotalMilliseconds < 400) return;
            if (!_everActive) return;
            if ((DateTime.Now - _activatedAt).TotalMilliseconds < 400)
            {
                ScheduleActivation();
                return;
            }
            HidePanel(false);
        }
        private void InstallHooks()
        {
            if (Hooks.IsInstalled) return;
            bool ok = Hooks.Install(OnGlobalKey);
            Log.Write("hooks installed=" + ok);
        }

        private void UpdateHoverFromCursor()
        {
            if (!IsVisible) return;

            Native.POINT sp;
            if (!Native.GetCursorPos(out sp)) return;
            if (_hasCursorPoint && sp.X == _cursorX && sp.Y == _cursorY) return;
            _hasCursorPoint = true;
            _cursorX = sp.X;
            _cursorY = sp.Y;

            Point p = PointFromScreen(new Point(sp.X, sp.Y));
            if (p.X < 0 || p.Y < 0 || p.X >= ActualWidth || p.Y >= ActualHeight) return;

            RowView row = HitRow(p);
            if (row != null) SelectIndex(row.Index);
        }

        private void OnPanelMouseMove(object sender, MouseEventArgs e)
        {
            RowView row = HitRow(e.GetPosition(this));
            if (row != null) SelectIndex(row.Index);
        }

        private void OnPanelMouseLeave(object sender, MouseEventArgs e)
        {
            // 选中不再跟随鼠标离开而消失：鼠标与键盘共用同一个选中态
        }

        private RowView HitRow(Point p)
        {
            IInputElement hit = InputHitTest(p);
            DependencyObject d = hit as DependencyObject;
            while (d != null)
            {
                Border b = d as Border;
                if (b != null && b.Tag is RowView) return (RowView)b.Tag;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private void SelectIndex(int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            if (_selected == index) return;
            _selected = index;
            UpdateSelection();
        }

        private void SyncCursorPoint()
        {
            Native.POINT sp;
            if (Native.GetCursorPos(out sp))
            {
                _cursorX = sp.X;
                _cursorY = sp.Y;
                _hasCursorPoint = true;
            }
            else
            {
                _hasCursorPoint = false;
            }
        }

        private Border MakeFade(bool top)
        {
            Border b = new Border();
            b.Height = top ? 12 : 22;
            b.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.IsHitTestVisible = false;
            b.Opacity = 0;
            // 跟随面板 10px 圆角（内容区在 1px 边框内，内圆角按 9 算）
            b.CornerRadius = top ? new CornerRadius(9, 9, 0, 0) : new CornerRadius(0, 0, 9, 9);
            return b;
        }

        private void UpdateFadeBrushes()
        {
            if (_topFade == null || _bottomFade == null) return;
            Color c = Theme.PanelSolidBrush.Color;
            Color clear = Color.FromArgb(0, c.R, c.G, c.B);
            Color solid = Color.FromArgb(255, c.R, c.G, c.B);

            LinearGradientBrush topBrush = new LinearGradientBrush(solid, clear, new Point(0, 0), new Point(0, 1));
            topBrush.Freeze();
            _topFade.Background = topBrush;

            LinearGradientBrush bottomBrush = new LinearGradientBrush(clear, solid, new Point(0, 0), new Point(0, 1));
            bottomBrush.Freeze();
            _bottomFade.Background = bottomBrush;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scroll != null)
                Log.Write("scroll " + _scroll.VerticalOffset.ToString("0") + "/" + _scroll.ScrollableHeight.ToString("0"));
            UpdateFadeState();
        }

        private void UpdateFadeState()
        {
            if (_scroll == null || _topFade == null || _bottomFade == null) return;

            double offset = _scroll.VerticalOffset;
            double max = _scroll.ScrollableHeight;
            double top = offset > 1 ? 1 : 0;
            double bottom = offset < max - 1 ? 1 : 0;

            if (!_topFade.Opacity.Equals(top)) _topFade.Opacity = top;
            if (!_bottomFade.Opacity.Equals(bottom))
            {
                _bottomFade.Opacity = bottom;
                Log.Write("scroll offset=" + offset.ToString("0") + " max=" + max.ToString("0") + " more=" + (bottom > 0));
            }
        }

        private bool OnGlobalKey(int vk)
        {
            if (!IsVisible || IsHiding) return false;
            if (Hooks.ModifierDown) return false;

            if (vk == 0x1B)
            {
                Dispatcher.BeginInvoke(new Action(delegate { HidePanel(false); }));
                return true;
            }
            if (vk == 0x26)
            {
                Dispatcher.BeginInvoke(new Action(delegate { MoveSelection(-1); }));
                return true;
            }
            if (vk == 0x28)
            {
                Dispatcher.BeginInvoke(new Action(delegate { MoveSelection(1); }));
                return true;
            }
            if (vk == 0x0D)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (_selected >= 0 && _selected < _rows.Count) ActivateWindow(_rows[_selected]);
                }));
                return true;
            }

            string token = Hooks.TokenForVk(vk);
            if (token == null) return false;
            Dispatcher.BeginInvoke(new Action(delegate { HandleGlobalToken(token); }));
            return true;
        }

        private void HandleGlobalToken(string token)
        {
            if (!IsVisible || IsHiding) return;
            TryToken(token);
        }
        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            RowView row = HitRow(e.GetPosition(this));
            if (row != null)
            {
                SelectIndex(row.Index);
                ShowRowMenu(row);
            }
            else
            {
                ShowMenu();
            }
        }

        /// <summary>
        /// 行上右键：窗口操作菜单（关闭 / 最小化 / 最大化 / 置顶 / 移到副屏 / 复制标题）
        /// </summary>
        private void ShowRowMenu(RowView row)
        {
            if (row == null || row.Hwnd == IntPtr.Zero) return;

            Popup popup = NewMenuPopup();
            Border box = NewMenuBox(150);
            StackPanel stack = new StackPanel();

            stack.Children.Add(MenuRow("关闭窗口", delegate
            {
                popup.IsOpen = false;
                if (WindowList.CloseWindow(row.Hwnd)) DelayedRefresh();
                else ShowStatus("无法关闭该窗口");
            }));
            stack.Children.Add(MenuRow("最小化", delegate
            {
                popup.IsOpen = false;
                if (WindowList.Minimize(row.Hwnd)) HidePanel(false);
                else ShowStatus("无法最小化该窗口");
            }));
            stack.Children.Add(MenuRow("最大化 / 还原", delegate
            {
                popup.IsOpen = false;
                if (WindowList.ToggleMaximize(row.Hwnd)) HidePanel(false);
                else ShowStatus("无法调整该窗口");
            }));
            stack.Children.Add(MenuRow("置顶 / 取消置顶", delegate
            {
                popup.IsOpen = false;
                if (WindowList.ToggleTopMost(row.Hwnd)) DelayedRefresh();
                else ShowStatus("无法修改置顶状态");
            }));
            stack.Children.Add(MenuRow("移到下一块屏", delegate
            {
                popup.IsOpen = false;
                if (WindowList.MoveToNextMonitor(row.Hwnd)) HidePanel(false);
                else ShowStatus("只有一块屏幕，或无法移动");
            }));
            stack.Children.Add(MenuRow("复制标题", delegate
            {
                popup.IsOpen = false;
                try
                {
                    Clipboard.SetText(row.Title ?? "");
                    ShowStatus("已复制标题");
                }
                catch
                {
                    ShowStatus("复制失败");
                }
            }));

            box.Child = stack;
            popup.Child = box;
            popup.IsOpen = true;
        }

        private Popup NewMenuPopup()
        {
            Popup popup = new Popup();
            popup.PlacementTarget = this;
            popup.Placement = PlacementMode.MousePoint;
            popup.StaysOpen = false;
            popup.AllowsTransparency = true;
            popup.Opened += delegate { _suppressDeactivate = true; };
            popup.Closed += delegate
            {
                _suppressDeactivate = false;
                if (!IsActive) HidePanel(false);
            };
            return popup;
        }

        private Border NewMenuBox(double minWidth)
        {
            Border box = new Border();
            box.Background = Theme.MenuBgBrush;
            box.BorderBrush = Theme.PanelBorderBrush;
            box.BorderThickness = new Thickness(1);
            box.CornerRadius = new CornerRadius(8);
            box.Padding = new Thickness(4);
            box.MinWidth = minWidth;
            return box;
        }

        private void DelayedRefresh()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(250);
            timer.Tick += delegate
            {
                timer.Stop();
                if (IsVisible) RefreshRowsInPlace();
            };
            timer.Start();
        }

        private void ShowMenu()
        {
            Popup popup = NewMenuPopup();
            Border box = NewMenuBox(140);
            StackPanel stack = new StackPanel();
            stack.Children.Add(MenuRow("设置", delegate
            {
                popup.IsOpen = false;
                Dispatcher.BeginInvoke(new Action(OpenSettings));
            }));
            stack.Children.Add(MenuRow("退出", delegate
            {
                popup.IsOpen = false;
                Application.Current.Shutdown();
            }));
            box.Child = stack;
            popup.Child = box;
            popup.IsOpen = true;
        }

        private Border MenuRow(string text, Action action)
        {
            Border b = new Border();
            b.Padding = new Thickness(12, 7, 12, 7);
            b.CornerRadius = new CornerRadius(4);
            b.Background = Theme.PanelSolidBrush;
            b.Cursor = Cursors.Hand;
            b.Child = Ui.Label(text, 12.5, Theme.TextPrimaryBrush);
            b.MouseEnter += delegate { b.Background = Theme.RowHoverBrush; };
            b.MouseLeave += delegate { b.Background = Theme.PanelSolidBrush; };
            b.MouseLeftButtonUp += delegate { action(); };
            return b;
        }

        public void OpenSettings()
        {
            if (_settingsOpen) return;
            _settingsOpen = true;
            _suppressDeactivate = true;
            HidePanel(true);
            try
            {
                SettingsWindow w = new SettingsWindow(this);
                w.Closed += delegate
                {
                    _settingsOpen = false;
                    _suppressDeactivate = false;
                    if (Activity != null) Activity(this, EventArgs.Empty);
                };
                w.Show();
            }
            catch (Exception ex)
            {
                _settingsOpen = false;
                _suppressDeactivate = false;
                Log.Exception("OpenSettings", ex);
                MessageBox.Show(ex.Message, "SmartLauncher");
            }
        }
        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hwnd);
            if (source != null)
            {
                source.AddHook(WndProc);
            }
        }

        private void OnSystemThemeChanged()
        {
            if (!Theme.Load()) return;
            Background = Brushes.Transparent;
            _panel.Background = Theme.PanelSolidBrush;
            _panel.BorderBrush = Theme.PanelBorderBrush;
            BuildWindowRows();
            UpdateFadeBrushes();
            if (IsVisible)
            {
                UpdateLayout();
                PositionNearCursor();
                UpdateFadeState();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_SETTINGCHANGE && lParam != IntPtr.Zero)
            {
                string s = Marshal.PtrToStringUni(lParam);
                if (string.Equals(s, "ImmersiveColorSet", StringComparison.OrdinalIgnoreCase))
                    Dispatcher.BeginInvoke(new Action(OnSystemThemeChanged));
            }
            else if (msg == Native.WM_DPICHANGED && IsVisible)
            {
                Dispatcher.BeginInvoke(new Action(PositionNearCursor));
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            Hooks.Uninstall();
            SetState(PanelState.Hidden);
            _hoverTimer.Stop();
            _statusTimer.Stop();
            _trimTimer.Stop();
            _focusTimer.Stop();
            _activationTimer.Stop();
            _clickTimer.Stop();
            _leftWasDown = false;
            base.OnClosed(e);
        }

    }
}
