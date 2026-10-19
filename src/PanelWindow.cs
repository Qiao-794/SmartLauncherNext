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
        private const double PanelWidth = 243;

        private AppConfig _config;
        private bool _pinned;
        private readonly StackPanel _list;
        private readonly Border _panel;
        private readonly Grid _content;
        private readonly Border _status;
        private readonly TextBlock _statusText;
        private readonly List<RowView> _rows = new List<RowView>();
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
        private readonly DispatcherTimer _autoHideTimer;
        private readonly DispatcherTimer _activationTimer;
        private bool _everActive;
        private int _focusAttempts;
        private DateTime _activatedAt;
        private RowView _hoverRow;
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
            ShowActivated = false;
            AllowsTransparency = false;
            Background = Theme.PanelSolidBrush;
            Width = PanelWidth;
            Height = 473;
            SizeToContent = SizeToContent.Manual;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "SmartLauncherNextPanel";
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            FontFamily = Theme.UiFont;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(7);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            Grid root = new Grid();
            Content = root;

            _panel = new Border();
            _panel.Margin = new Thickness(0);
            _panel.CornerRadius = new CornerRadius(7);
            _panel.BorderThickness = new Thickness(1);
            _panel.BorderBrush = Theme.PanelBorderBrush;
            _panel.Background = Theme.PanelSolidBrush;
            _panel.SnapsToDevicePixels = true;
            root.Children.Add(_panel);

            _content = new Grid();
            _content.RenderTransformOrigin = new Point(0.5, 0.5);
            _panel.Child = _content;
            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Focusable = false;
            scroll.Padding = new Thickness(0);
            scroll.Background = Theme.PanelSolidBrush;
            _content.Children.Add(scroll);

            _list = new StackPanel();
            _list.Margin = new Thickness(4.5, 5.5, 4.5, 5.5);
            scroll.Content = _list;

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

            _autoHideTimer = new DispatcherTimer();
            _autoHideTimer.Interval = TimeSpan.FromSeconds(6);
            _autoHideTimer.Tick += delegate
            {
                _autoHideTimer.Stop();
                if (!IsActive && !_pinned) HidePanel(false);
            };

            _activationTimer = new DispatcherTimer();
            _activationTimer.Interval = TimeSpan.FromMilliseconds(350);
            _activationTimer.Tick += delegate { OnActivationTimer(); };

            Activated += delegate
            {
                _everActive = true;
                _activatedAt = DateTime.Now;
                _focusTimer.Stop();
                _activationTimer.Stop();
                _autoHideTimer.Stop();
            };

            BuildRows();
            Log.Write("Panel rows built rows=" + _rows.Count + " height=" + Height);

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
            BuildRows();
            if (IsVisible)
            {
                UpdateLayout();
                PositionNearCursor();
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
                SetState(PanelState.Visible);
                if (!IsActive) ScheduleActivation();
                return;
            }

            if (Theme.Load())
            {
                BuildRows();
            }
            _panel.Background = Theme.PanelSolidBrush;
            _panel.BorderBrush = Theme.PanelBorderBrush;
            Background = Theme.PanelSolidBrush;
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
            InstallHooks();
            _hoverTimer.Stop();
            _hoverTimer.Start();
            Topmost = true;
            _status.Visibility = Visibility.Collapsed;
            _selected = -1;
            UpdateSelection();

            if (Activity != null) Activity(this, EventArgs.Empty);
        }
        public void HideNow()
        {
            HidePanel(true);
        }

        private void ActivatePanel()
        {
            Activate();
            Focus();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Native.ForceForeground(hwnd);
            Keyboard.Focus(this);
        }

        private void HidePanel(bool immediate)
        {
            if (!IsVisible || IsHiding) return;
            Hooks.Uninstall();
            _hoverTimer.Stop();
            SetHover(null);
            _focusTimer.Stop();
            _activationTimer.Stop();
            _autoHideTimer.Stop();
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
            System.Drawing.Rectangle work = Native.GetWorkAreaPixels();

            double scale = 1.0;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                uint dpi = Native.GetDpiForWindow(hwnd);
                if (dpi > 0) scale = dpi / 96.0;
            }

            double contentW = PanelWidth;
            double contentH = Height;
            if (contentH <= 0) contentH = 473;

            double wpx = contentW * scale;
            double hpx = contentH * scale;
            double x = pt.X - 24 * scale;
            double y = pt.Y + 14 * scale;

            if (x + wpx > work.Right - 8) x = work.Right - wpx - 8;
            if (x < work.Left + 8) x = work.Left + 8;
            if (y + hpx > work.Bottom - 8) y = pt.Y - hpx - 16 * scale;
            if (y < work.Top + 8) y = work.Top + 8;

            Left = x / scale;
            Top = y / scale;
        }

        private void Launch(RowView row)
        {
            if (row == null) return;
            string error;
            if (Launcher.TryRun(row.Item, out error))
            {
                HidePanel(false);
            }
            else
            {
                ShowStatus(error);
            }
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
                if (_selected >= 0 && _selected < _rows.Count) Launch(_rows[_selected]);
                e.Handled = true;
                return;
            }

            string token = KeyToToken(e.Key);
            if (token == null) return;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].Item.Key, token, StringComparison.OrdinalIgnoreCase))
                {
                    e.Handled = true;
                    Launch(_rows[i]);
                    return;
                }
            }
        }

        private void BeginFocusGrace()
        {
            _everActive = false;
            _focusAttempts = 0;
            _focusGraceUntil = DateTime.Now.AddMilliseconds(2500);
            _focusTimer.Stop();
            _activationTimer.Stop();
            _activationTimer.Start();
            _autoHideTimer.Stop();
            if (!_pinned) _autoHideTimer.Start();
        }

        private void ScheduleActivation()
        {
            _focusAttempts = 0;
            _focusGraceUntil = DateTime.Now.AddMilliseconds(2500);
            _focusTimer.Stop();
            _activationTimer.Stop();
            _activationTimer.Start();
            _autoHideTimer.Stop();
            if (!_pinned) _autoHideTimer.Start();
        }

        private void OnActivationTimer()
        {
            _activationTimer.Stop();
            if (IsActive)
            {
                _everActive = true;
                _autoHideTimer.Stop();
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
                _everActive = true;
                _autoHideTimer.Stop();
                return;
            }

            _focusAttempts++;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Native.ForceForeground(hwnd);
            if (IsActive)
            {
                _everActive = true;
                _autoHideTimer.Stop();
                return;
            }
            if (_focusAttempts < 8) _focusTimer.Start();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            Log.Write("Deactivated pinned=" + _pinned + " elapsed=" + (DateTime.Now - _shownAt).TotalMilliseconds + " everActive=" + _everActive);
            if (_pinned || _suppressDeactivate || _settingsOpen) return;
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

            RowView target = null;
            Native.POINT sp;
            if (Native.GetCursorPos(out sp))
            {
                Point p = PointFromScreen(new Point(sp.X, sp.Y));
                if (p.X >= 0 && p.Y >= 0 && p.X < ActualWidth && p.Y < ActualHeight)
                {
                    IInputElement hit = InputHitTest(p);
                    DependencyObject d = hit as DependencyObject;
                    while (d != null)
                    {
                        Border b = d as Border;
                        if (b != null && b.Tag is RowView)
                        {
                            target = (RowView)b.Tag;
                            break;
                        }
                        d = VisualTreeHelper.GetParent(d);
                    }
                }
            }
            SetHover(target);
        }
        private void OnPanelMouseMove(object sender, MouseEventArgs e)
        {
            RowView target = null;
            IInputElement hit = InputHitTest(e.GetPosition(this));
            DependencyObject d = hit as DependencyObject;
            while (d != null)
            {
                Border b = d as Border;
                if (b != null && b.Tag is RowView)
                {
                    target = (RowView)b.Tag;
                    break;
                }
                d = VisualTreeHelper.GetParent(d);
            }
            SetHover(target);
        }

        private void OnPanelMouseLeave(object sender, MouseEventArgs e)
        {
            SetHover(null);
        }
        private void SetHover(RowView target)
        {
            if (_hoverRow == target) return;
            if (_hoverRow != null)
            {
                _hoverRow.Hover = false;
                _hoverRow.Apply();
            }
            _hoverRow = target;
            if (_hoverRow != null)
            {
                _hoverRow.Hover = true;
                _hoverRow.Apply();
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
                    if (_selected >= 0 && _selected < _rows.Count) Launch(_rows[_selected]);
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
            for (int i = 0; i < _rows.Count; i++)
            {
                if (string.Equals(_rows[i].Item.Key, token, StringComparison.OrdinalIgnoreCase))
                {
                    Launch(_rows[i]);
                    return;
                }
            }
        }
        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ShowMenu();
        }

        private void ShowMenu()
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

            Border box = new Border();
            box.Background = Theme.MenuBgBrush;
            box.BorderBrush = Theme.PanelBorderBrush;
            box.BorderThickness = new Thickness(1);
            box.CornerRadius = new CornerRadius(6);
            box.Padding = new Thickness(4);
            box.MinWidth = 140;

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
            ApplyNativeFrame();
        }

        private void ApplyNativeFrame()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dark = Theme.IsDark ? 1 : 0;
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
            int corner = Native.DWMWCP_ROUND;
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
            int none = unchecked((int)0xFFFFFFFE);
            Native.DwmSetWindowAttribute(hwnd, 34, ref none, 4);
        }

        private void OnSystemThemeChanged()
        {
            if (!Theme.Load()) return;
            Background = Theme.PanelSolidBrush;
            _panel.Background = Theme.PanelSolidBrush;
            _panel.BorderBrush = Theme.PanelBorderBrush;
            ApplyNativeFrame();
            BuildRows();
            if (IsVisible)
            {
                UpdateLayout();
                PositionNearCursor();
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
            _autoHideTimer.Stop();
            base.OnClosed(e);
        }

    }
}
