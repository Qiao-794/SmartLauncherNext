using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Win32;

namespace SmartLauncher
{
    public partial class SettingsWindow : Window
    {
        private static readonly GridLength[] Cols = new GridLength[]
        {
            new GridLength(94),
            new GridLength(72),
            new GridLength(150),
            new GridLength(112),
            new GridLength(1, GridUnitType.Star),
            new GridLength(86),
            new GridLength(68)
        };

        private readonly PanelWindow _panel;
        private AppConfig _working;
        private readonly StackPanel _rowsPanel;
        private readonly List<RowEditor> _editors = new List<RowEditor>();
        private readonly TextBlock _countText;
        private readonly TextBlock _statusText;
        private readonly CheckBox _idleBox;
        private TextBox _filterBox;
        private RowEditor _focusedEditor;
        private RowEditor _dragEditor;
        private Point _dragStart;
        private bool _dragging;
        private bool _dirty;

        public SettingsWindow(PanelWindow panel)
        {
            Log.Write("Settings ctor start");
            _panel = panel;
            _working = ConfigStore.Clone(Program.Config);
            Theme.Load();

            Title = "SmartLauncher 设置";
            Width = 1040;
            Height = 720;
            MinWidth = 900;
            MinHeight = 560;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            AllowsTransparency = false;
            Background = Theme.WindowSolidBrush;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = true;
            FontFamily = Theme.UiFont;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 44;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            Border root = new Border();
            root.Background = Theme.WindowSolidBrush;
            root.CornerRadius = new CornerRadius(8);
            Content = root;

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            root.Child = grid;

            UIElement title = BuildTitleBar();
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            UIElement toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 1);
            grid.Children.Add(toolbar);

            UIElement header = BuildHeader();
            Grid.SetRow(header, 2);
            grid.Children.Add(header);

            _rowsPanel = new StackPanel();
            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Focusable = false;
            scroll.Margin = new Thickness(16, 4, 16, 0);
            scroll.Background = Brushes.Transparent;
            scroll.Resources[typeof(ScrollBar)] = Ui.ScrollBarStyle();
            scroll.Padding = new Thickness(0, 4, 0, 4);
            scroll.Content = _rowsPanel;
            Grid.SetRow(scroll, 3);
            grid.Children.Add(scroll);

            _countText = new TextBlock();
            _countText.FontSize = 11.5;
            _countText.Foreground = Theme.TextSecondaryBrush;
            _countText.VerticalAlignment = VerticalAlignment.Center;
            _countText.Margin = new Thickness(0, 0, 16, 0);

            _statusText = new TextBlock();
            _statusText.FontSize = 11.5;
            _statusText.Foreground = Theme.TextSecondaryBrush;
            _statusText.VerticalAlignment = VerticalAlignment.Center;
            _statusText.TextTrimming = TextTrimming.CharacterEllipsis;
            _statusText.HorizontalAlignment = HorizontalAlignment.Right;
            _statusText.Margin = new Thickness(12, 0, 12, 0);

            _idleBox = new CheckBox();
            _idleBox.Content = "空闲 5 分钟后退出后台";
            _idleBox.Style = Ui.CheckBoxStyle();
            _idleBox.VerticalAlignment = VerticalAlignment.Center;
            _idleBox.Checked += delegate { _dirty = true; };
            _idleBox.Unchecked += delegate { _dirty = true; };

            UIElement footer = BuildFooter();
            Grid.SetRow(footer, 4);
            grid.Children.Add(footer);

            _idleBox.IsChecked = _working.IdleExitMinutes > 0;

            LoadRows();
            _idleBox.IsChecked = _working.IdleExitMinutes > 0;
            _dirty = false;

            Log.Write("Settings ctor end");
            Closing += OnClosing;
            SourceInitialized += OnSourceInitialized;
            Activated += delegate { Topmost = true; };
            Deactivated += delegate { Topmost = false; };
            Loaded += delegate
            {
                Activate();
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                Native.ForceForeground(hwnd);
                Topmost = true;
                Topmost = false;
                Activate();
            };
        }















        private void LoadRows()
        {
            _rowsPanel.Children.Clear();
            _editors.Clear();

            int index = 0;
            if (_working != null && _working.Categories != null)
            {
                foreach (Category cat in _working.Categories)
                {
                    if (cat == null || cat.Items == null) continue;
                    foreach (Item item in cat.Items)
                    {
                        if (item == null) continue;
                        AddEditor(item, cat.Name, index);
                        index++;
                    }
                }
            }
            UpdateCount();
            Restripe();
        }

        private void AddEditor(Item item, string category, int index)
        {
            RowEditor ed = new RowEditor();
            ed.Item = item;
            ed.Category = category == null ? "" : category;

            Border row = new Border();
            row.Height = 42;
            row.BorderBrush = Theme.SeparatorBrush;
            row.BorderThickness = new Thickness(0, 0, 0, 1);
            row.Padding = new Thickness(0, 6, 0, 6);
            row.Background = index % 2 == 1 ? Theme.RowAltBrush : Brushes.Transparent;
            ed.Container = row;

            Grid g = ColumnsGrid();

            Grid catGrid = new Grid();
            catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) });
            catGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border grip = new Border();
            grip.Width = 14;
            grip.Background = Brushes.Transparent;
            grip.Cursor = Cursors.SizeNS;
            grip.ToolTip = "拖动排序";
            grip.VerticalAlignment = VerticalAlignment.Stretch;
            TextBlock gripGlyph = new TextBlock();
            gripGlyph.Text = "≡";
            gripGlyph.FontSize = 12;
            gripGlyph.Foreground = Theme.TextSecondaryBrush;
            gripGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            gripGlyph.VerticalAlignment = VerticalAlignment.Center;
            grip.Child = gripGlyph;
            Grid.SetColumn(grip, 0);
            catGrid.Children.Add(grip);
            grip.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { BeginRowDrag(ed, (UIElement)sender, e); };
            grip.PreviewMouseMove += delegate(object sender, MouseEventArgs e) { ContinueRowDrag(ed, (UIElement)sender, e); };
            grip.PreviewMouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) { EndRowDrag((UIElement)sender, e); };
            grip.LostMouseCapture += delegate { _dragEditor = null; _dragging = false; };

            TextBox catBox = MakeTextBox(ed.Category);
            catBox.FontSize = 12;
            catBox.ToolTip = "分组名称，可自定义";
            catBox.TextChanged += DirtyHandler;
            Grid.SetColumn(catBox, 1);
            catGrid.Children.Add(catBox);
            Put(g, catGrid, 0);
            ed.CategoryBox = catBox;

            TextBox keyBox = MakeTextBox(item.Key);
            keyBox.Width = 52;
            keyBox.HorizontalAlignment = HorizontalAlignment.Left;
            keyBox.TextAlignment = TextAlignment.Center;
            keyBox.FontWeight = FontWeights.SemiBold;
            keyBox.IsReadOnly = true;
            keyBox.Cursor = Cursors.Hand;
            keyBox.ToolTip = "点击后按一个键（A-Z、0-9、F1-F12），Delete 清除";
            keyBox.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { CaptureKey(ed, e); };
            keyBox.GotFocus += delegate { keyBox.SelectAll(); };
            Put(g, keyBox, 1);
            ed.KeyBox = keyBox;

            TextBox nameBox = MakeTextBox(item.Name);
            nameBox.TextChanged += DirtyHandler;
            Put(g, nameBox, 2);
            ed.NameBox = nameBox;

            Button typeButton = new Button();
            typeButton.Content = TypeLabel(item.Type) + "  ▾";
            typeButton.Style = Ui.ButtonStyle(false);
            typeButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            typeButton.Padding = new Thickness(8, 0, 8, 0);
            typeButton.FontSize = 12;
            typeButton.Height = 28;
            typeButton.Click += delegate { ShowTypeMenu(ed); };
            Put(g, typeButton, 3);
            ed.TypeButton = typeButton;

            Grid pathGrid = new Grid();
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            TextBox pathBox = MakeTextBox(item.Path);
            pathBox.TextChanged += delegate { _dirty = true; UpdateWatermark(ed); };
            pathGrid.Children.Add(pathBox);
            ed.PathBox = pathBox;

            TextBlock watermark = new TextBlock();
            watermark.FontSize = 12;
            watermark.Foreground = Theme.TextSecondaryBrush;
            watermark.Margin = new Thickness(9, 0, 0, 0);
            watermark.VerticalAlignment = VerticalAlignment.Center;
            watermark.IsHitTestVisible = false;
            watermark.TextTrimming = TextTrimming.CharacterEllipsis;
            pathGrid.Children.Add(watermark);
            ed.PathWatermark = watermark;

            Button browse = new Button();
            browse.Content = "…";
            browse.Width = 26;
            browse.Height = 28;
            browse.Margin = new Thickness(4, 0, 0, 0);
            browse.Padding = new Thickness(0);
            browse.Style = Ui.ButtonStyle(false);
            browse.FontSize = 12;
            browse.Click += delegate { Browse(ed); };
            Grid.SetColumn(browse, 1);
            pathGrid.Children.Add(browse);
            ed.BrowseButton = browse;

            Put(g, pathGrid, 4);

            StackPanel iconPanel = new StackPanel();
            iconPanel.Orientation = Orientation.Horizontal;
            iconPanel.VerticalAlignment = VerticalAlignment.Center;

            Border iconBorder = new Border();
            iconBorder.Width = 28;
            iconBorder.Height = 28;
            iconBorder.CornerRadius = new CornerRadius(4);
            iconBorder.Background = Theme.ControlBgBrush;
            iconBorder.Margin = new Thickness(0, 0, 6, 0);
            iconBorder.ToolTip = "右键清除自定义图标";
            Image iconImage = new Image();
            iconImage.Width = 24;
            iconImage.Height = 24;
            iconImage.Stretch = Stretch.Uniform;
            iconImage.Source = IconService.Get(item);
            iconBorder.Child = iconImage;
            iconBorder.MouseRightButtonUp += delegate
            {
                ed.Item.IconPath = "";
                ed.RefreshIcon();
                _dirty = true;
            };
            iconPanel.Children.Add(iconBorder);
            ed.IconImage = iconImage;

            Button choose = new Button();
            choose.Content = "选择";
            choose.Width = 38;
            choose.Height = 24;
            choose.Padding = new Thickness(0);
            choose.FontSize = 10.5;
            choose.Style = Ui.ButtonStyle(false);
            choose.Click += delegate { ChooseIcon(ed); };
            iconPanel.Children.Add(choose);
            Put(g, iconPanel, 5);

            StackPanel ops = new StackPanel();
            ops.Orientation = Orientation.Horizontal;
            ops.VerticalAlignment = VerticalAlignment.Center;

            Button test = new Button();
            test.Content = "▶";
            test.Width = 24;
            test.Height = 24;
            test.Padding = new Thickness(0);
            test.FontSize = 9.5;
            test.Style = Ui.ButtonStyle(false);
            test.ToolTip = "试运行这一行";
            test.Margin = new Thickness(0, 0, 4, 0);
            test.Click += delegate { _focusedEditor = ed; TestRow(ed); };
            ops.Children.Add(test);

            Button del = new Button();
            del.Content = "✕";
            del.Width = 24;
            del.Height = 24;
            del.Padding = new Thickness(0);
            del.FontSize = 11;
            del.Style = Ui.ButtonStyle(false, true);
            del.ToolTip = "删除这一行";
            del.Click += delegate { DeleteEditor(ed); };
            ops.Children.Add(del);
            Put(g, ops, 6);

            row.PreviewMouseDown += delegate { _focusedEditor = ed; };
            row.Child = g;
            _rowsPanel.Children.Add(row);
            _editors.Add(ed);

            UpdateTypeUi(ed);
            UpdateWatermark(ed);
        }

        private TextBox MakeTextBox(string text)
        {
            TextBox t = new TextBox();
            t.Text = text == null ? "" : text;
            t.Style = Ui.TextBoxStyle();
            t.FontSize = 12;
            t.Height = 28;
            t.Padding = new Thickness(8, 0, 8, 0);
            t.VerticalContentAlignment = VerticalAlignment.Center;
            return t;
        }



        private void DirtyHandler(object sender, TextChangedEventArgs e)
        {
            _dirty = true;
        }

        private static string TypeLabel(string type)
        {
            if (type == "exe") return "本地程序";
            if (type == "url") return "网址";
            return "Quicker 动作";
        }

        private void ShowTypeMenu(RowEditor ed)
        {
            Popup popup = new Popup();
            popup.PlacementTarget = ed.TypeButton;
            popup.Placement = PlacementMode.Bottom;
            popup.StaysOpen = false;
            popup.AllowsTransparency = true;

            Border box = new Border();
            box.Background = Theme.MenuBgBrush;
            box.BorderBrush = Theme.CardSolidBorder;
            box.BorderThickness = new Thickness(1);
            box.CornerRadius = new CornerRadius(6);
            box.Padding = new Thickness(4);
            box.MinWidth = 170;
            box.Effect = Ui.Shadow();

            StackPanel stack = new StackPanel();
            stack.Children.Add(TypeRow(ed, popup, "Quicker 动作", "quicker"));
            stack.Children.Add(TypeRow(ed, popup, "本地程序", "exe"));
            stack.Children.Add(TypeRow(ed, popup, "网址", "url"));
            box.Child = stack;
            popup.Child = box;
            popup.IsOpen = true;
        }

        private Border TypeRow(RowEditor ed, Popup popup, string label, string type)
        {
            Border b = new Border();
            b.Padding = new Thickness(10, 6, 10, 6);
            b.CornerRadius = new CornerRadius(4);
            b.Background = Brushes.Transparent;
            b.Cursor = Cursors.Hand;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock t = new TextBlock();
            t.Text = label;
            t.FontSize = 12.5;
            t.Foreground = Theme.TextPrimaryBrush;
            g.Children.Add(t);

            TextBlock check = new TextBlock();
            check.Text = "✓";
            check.FontSize = 12;
            check.Foreground = Theme.AccentBrush;
            check.Visibility = ed.Item.Type == type ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(check, 1);
            g.Children.Add(check);

            b.Child = g;
            b.MouseEnter += delegate { b.Background = Theme.RowHoverBrush; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate
            {
                ed.Item.Type = type;
                UpdateTypeUi(ed);
                UpdateWatermark(ed);
                _dirty = true;
                popup.IsOpen = false;
            };
            return b;
        }

        private void UpdateTypeUi(RowEditor ed)
        {
            string type = ed.Item.Type;
            ed.TypeButton.Content = TypeLabel(type) + "  ▾";
            if (type == "exe")
            {
                ed.BrowseButton.Visibility = Visibility.Visible;
                ed.BrowseButton.Content = "…";
                ed.BrowseButton.ToolTip = "选择程序";
            }
            else if (type == "url")
            {
                ed.BrowseButton.Visibility = Visibility.Visible;
                ed.BrowseButton.Content = "↗";
                ed.BrowseButton.ToolTip = "在浏览器中测试";
            }
            else
            {
                ed.BrowseButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateWatermark(RowEditor ed)
        {
            string type = ed.Item.Type;
            string hint;
            if (type == "exe") hint = @"例如 D:\Apps\App.exe";
            else if (type == "url") hint = "https://example.com";
            else hint = "Quicker 动作 ID 或名称";
            ed.PathWatermark.Text = hint;
            ed.PathWatermark.Visibility = string.IsNullOrEmpty(ed.PathBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ed.PathBox.ToolTip = hint;
        }

        private void CaptureKey(RowEditor ed, KeyEventArgs e)
        {
            if (e.Key == Key.Tab) return;

            if (e.Key == Key.Escape || e.Key == Key.Back || e.Key == Key.Delete)
            {
                ed.KeyBox.Text = "";
                ed.Item.Key = "";
                _dirty = true;
                e.Handled = true;
                return;
            }

            string token = KeyToken(e.Key);
            if (token == null)
            {
                e.Handled = true;
                return;
            }

            foreach (RowEditor other in _editors)
            {
                if (other == ed) continue;
                if (string.Equals(other.KeyBox.Text, token, StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("快捷键 " + token + " 已被「" + other.NameBox.Text + "」占用", true);
                    e.Handled = true;
                    return;
                }
            }

            ed.KeyBox.Text = token;
            ed.Item.Key = token;
            _dirty = true;
            e.Handled = true;
        }

        private static string KeyToken(Key key)
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

        private void Browse(RowEditor ed)
        {
            if (ed.Item.Type == "url")
            {
                string url = ed.PathBox.Text.Trim();
                if (url.Length == 0) return;
                if (url.IndexOf("://", StringComparison.Ordinal) < 0) url = "https://" + url;
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch (Exception ex) { SetStatus(ex.Message, true); }
                return;
            }

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "选择程序";
            dlg.Filter = "程序|*.exe|所有文件|*.*";
            if (dlg.ShowDialog(this) == true)
            {
                ed.PathBox.Text = dlg.FileName;
                ed.Item.Path = dlg.FileName;
                _dirty = true;
            }
        }

        private void ChooseIcon(RowEditor ed)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "选择图标";
            dlg.Filter = "图片|*.png;*.ico;*.jpg;*.jpeg;*.bmp|所有文件|*.*";
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                string relative = CopyIcon(dlg.FileName);
                ed.Item.IconPath = relative;
                ed.RefreshIcon();
                _dirty = true;
            }
            catch (Exception ex)
            {
                SetStatus("图标导入失败：" + ex.Message, true);
            }
        }

        private string CopyIcon(string source)
        {
            Directory.CreateDirectory(ConfigStore.IconDir);
            string name = "custom-" + DateTime.Now.Ticks.ToString("x") + ".png";
            string dest = Path.Combine(ConfigStore.IconDir, name);

            BitmapImage bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(source, UriKind.Absolute);
            bmp.EndInit();

            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (FileStream fs = File.Create(dest))
                enc.Save(fs);

            return @"Cache\Icons\" + name;
        }

        private void DeleteEditor(RowEditor ed)
        {
            _rowsPanel.Children.Remove(ed.Container);
            _editors.Remove(ed);
            if (_focusedEditor == ed) _focusedEditor = null;
            _dirty = true;
            UpdateCount();
            Restripe();
        }

        private void Restripe()
        {
            for (int i = 0; i < _editors.Count; i++)
                _editors[i].Container.Background = i % 2 == 1 ? Theme.RowAltBrush : Brushes.Transparent;
        }

        private void AddRow()
        {
            string category = "软件";
            if (_editors.Count > 0)
            {
                string last = _editors[_editors.Count - 1].CategoryBox.Text.Trim();
                if (last.Length > 0) category = last;
            }

            Item item = new Item();
            item.Name = "新建项目";
            item.Type = "quicker";
            item.Key = NextFreeKey();
            item.Path = "";
            item.IconPath = "";

            AddEditor(item, category, _editors.Count);
            Restripe();
            UpdateCount();
            _dirty = true;

            RowEditor ed = _editors[_editors.Count - 1];
            _focusedEditor = ed;
            ed.NameBox.Focus();
            ed.NameBox.SelectAll();
            ed.Container.BringIntoView();
            SetStatus("已添加一行，编辑后记得保存", false);
        }

        private string NextFreeKey()
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                bool used = false;
                foreach (RowEditor ed in _editors)
                    if (string.Equals(ed.KeyBox.Text, c.ToString(), StringComparison.OrdinalIgnoreCase)) { used = true; break; }
                if (!used) return c.ToString();
            }
            for (char c = '0'; c <= '9'; c++)
            {
                bool used = false;
                foreach (RowEditor ed in _editors)
                    if (string.Equals(ed.KeyBox.Text, c.ToString(), StringComparison.OrdinalIgnoreCase)) { used = true; break; }
                if (!used) return c.ToString();
            }
            return "";
        }

        private void ImportLegacy()
        {
            string source;
            AppConfig imported = ConfigStore.ImportLegacy(out source);
            if (imported == null)
            {
                SetStatus("没有找到旧配置（D:\\Program Files\\SmartLauncher_Lite）", true);
                return;
            }

            MessageBoxResult r = MessageBox.Show(this,
                "将用旧配置覆盖当前编辑内容（尚未保存到磁盘），继续吗？",
                "导入旧配置", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            _working = imported;
            IconService.Clear();
            LoadRows();
            _dirty = true;
            SetStatus("已导入 " + CountItems() + " 项，来源：" + source, false);
        }

        private void Revert()
        {
            if (_dirty)
            {
                MessageBoxResult r = MessageBox.Show(this,
                    "放弃当前修改并重新读取磁盘上的配置？",
                    "还原", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            bool imported;
            AppConfig cfg = ConfigStore.Load(out imported);
            _working = cfg;
            IconService.Clear();
            LoadRows();
            _dirty = false;
            SetStatus("已还原", false);
        }

        private void OpenDataDir()
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ConfigStore.BaseDir + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private int CountItems()
        {
            int n = 0;
            if (_working != null && _working.Categories != null)
                foreach (Category c in _working.Categories)
                    if (c != null && c.Items != null) n += c.Items.Count;
            return n;
        }

        private void UpdateCount()
        {
            if (_filterBox != null && _filterBox.Text.Trim().Length > 0)
            {
                ApplyFilter(_filterBox.Text);
                return;
            }
            _countText.Text = "共 " + _editors.Count + " 项";
        }

        private void ApplyFilter(string query)
        {
            string q = (query ?? "").Trim();
            int visible = 0;
            foreach (RowEditor ed in _editors)
            {
                bool show = q.Length == 0
                    || ed.NameBox.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || ed.CategoryBox.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || ed.PathBox.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                ed.Container.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                if (show) visible++;
            }
            if (_countText != null)
                _countText.Text = q.Length == 0
                    ? "共 " + _editors.Count + " 项"
                    : "筛选 " + visible + " / " + _editors.Count + " 项";
        }

        private void MoveEditorTo(RowEditor ed, int target)
        {
            int current = _editors.IndexOf(ed);
            if (current < 0 || target < 0 || target >= _editors.Count || current == target) return;
            _editors.RemoveAt(current);
            _editors.Insert(target, ed);
            _rowsPanel.Children.Remove(ed.Container);
            _rowsPanel.Children.Insert(target, ed.Container);
            _dirty = true;
            Restripe();
            UpdateCount();
            _focusedEditor = ed;
            Log.Write("drag reorder target=" + target);
        }

        private void BeginRowDrag(RowEditor ed, UIElement grip, MouseButtonEventArgs e)
        {
            if (_filterBox != null && _filterBox.Text.Trim().Length > 0)
            {
                SetStatus("筛选状态下请用 ↑/↓ 排序", true);
                e.Handled = true;
                return;
            }
            _focusedEditor = ed;
            _dragEditor = ed;
            _dragStart = e.GetPosition(_rowsPanel);
            _dragging = false;
            Log.Write("drag begin " + ed.NameBox.Text + " y=" + _dragStart.Y);
            grip.CaptureMouse();
            e.Handled = true;
        }

        private void ContinueRowDrag(RowEditor ed, UIElement grip, MouseEventArgs e)
        {
            if (_dragEditor != ed || e.LeftButton != MouseButtonState.Pressed) return;
            Point p = e.GetPosition(_rowsPanel);
            if (!_dragging && Math.Abs(p.Y - _dragStart.Y) > 5) _dragging = true;
            if (!_dragging) return;

            int current = _editors.IndexOf(ed);
            int target = current;
            for (int i = 0; i < _editors.Count; i++)
            {
                Border c = _editors[i].Container;
                if (c.Visibility != Visibility.Visible) continue;
                Point top = c.TranslatePoint(new Point(0, 0), _rowsPanel);
                if (p.Y < top.Y + c.ActualHeight / 2)
                {
                    target = i;
                    break;
                }
                target = i;
            }
            Log.Write("drag move y=" + p.Y + " target=" + target + " current=" + current);
            if (target != current) MoveEditorTo(ed, target);
            e.Handled = true;
        }

        private void EndRowDrag(UIElement grip, MouseButtonEventArgs e)
        {
            bool wasDragging = _dragging;
            _dragEditor = null;
            _dragging = false;
            if (grip.IsMouseCaptured) grip.ReleaseMouseCapture();
            if (wasDragging) SetStatus("顺序已更新", false);
            e.Handled = true;
        }
        private void MoveEditor(int delta)
        {
            RowEditor ed = _focusedEditor;
            if (ed == null)
            {
                SetStatus("先在表格里点选一行，再上移/下移", true);
                return;
            }
            int i = _editors.IndexOf(ed);
            int j = i + delta;
            if (i < 0 || j < 0 || j >= _editors.Count) return;

            _editors.RemoveAt(i);
            _editors.Insert(j, ed);
            _rowsPanel.Children.Remove(ed.Container);
            _rowsPanel.Children.Insert(j, ed.Container);
            _dirty = true;
            Restripe();
            UpdateCount();
            SetStatus(delta < 0 ? "已上移" : "已下移", false);
            ed.NameBox.Focus();
            _focusedEditor = ed;
        }

        private void SyncEditor(RowEditor ed)
        {
            ed.Item.Name = ed.NameBox.Text.Trim();
            ed.Item.Key = ed.KeyBox.Text.Trim().ToUpperInvariant();
            ed.Item.Path = ed.PathBox.Text.Trim();
        }

        private void TestRow(RowEditor ed)
        {
            SyncEditor(ed);
            string error;
            string label = string.IsNullOrEmpty(ed.Item.Name) ? ed.Item.Path : ed.Item.Name;
            if (Launcher.TryRun(ed.Item, out error))
                SetStatus("已启动：" + label, false);
            else
                SetStatus("启动失败：" + error, true);
        }

        private void SetStatus(string text, bool error)
        {
            _statusText.Text = text == null ? "" : text;
            _statusText.Foreground = error
                ? new SolidColorBrush(Color.FromRgb(0xE8, 0x6B, 0x5E))
                : Theme.TextSecondaryBrush;
        }

        private bool Validate()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _editors.Count; i++)
            {
                RowEditor ed = _editors[i];
                string name = ed.NameBox.Text.Trim();
                if (name.Length == 0)
                {
                    SetStatus("第 " + (i + 1) + " 行：名称不能为空", true);
                    ed.NameBox.Focus();
                    return false;
                }

                string key = ed.KeyBox.Text.Trim().ToUpperInvariant();
                if (key.Length > 0)
                {
                    if (!IsValidKey(key))
                    {
                        SetStatus("第 " + (i + 1) + " 行：快捷键格式不对", true);
                        ed.KeyBox.Focus();
                        return false;
                    }
                    if (!keys.Add(key))
                    {
                        SetStatus("快捷键 " + key + " 重复了", true);
                        ed.KeyBox.Focus();
                        return false;
                    }
                }

                string path = ed.PathBox.Text.Trim();
                if (ed.Item.Type == "quicker" && path.Length == 0)
                {
                    SetStatus("第 " + (i + 1) + " 行：请填写 Quicker 动作 ID 或名称", true);
                    ed.PathBox.Focus();
                    return false;
                }
                if (ed.Item.Type == "exe")
                {
                    if (path.Length == 0)
                    {
                        SetStatus("第 " + (i + 1) + " 行：请选择程序路径", true);
                        ed.PathBox.Focus();
                        return false;
                    }
                    string full = Environment.ExpandEnvironmentVariables(path);
                    if (!File.Exists(full))
                    {
                        SetStatus("第 " + (i + 1) + " 行：找不到程序 " + path, true);
                        ed.PathBox.Focus();
                        return false;
                    }
                }
                if (ed.Item.Type == "url" && path.Length == 0)
                {
                    SetStatus("第 " + (i + 1) + " 行：请填写网址", true);
                    ed.PathBox.Focus();
                    return false;
                }
            }
            return true;
        }

        private static bool IsValidKey(string key)
        {
            if (key.Length == 1)
            {
                char c = key[0];
                return (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
            }
            if (key.Length >= 2 && key[0] == 'F')
            {
                int n;
                if (!int.TryParse(key.Substring(1), out n)) return false;
                return n >= 1 && n <= 12;
            }
            return false;
        }

        private AppConfig BuildConfig()
        {
            AppConfig result = new AppConfig();
            result.HideWhenInactive = _working.HideWhenInactive;
            result.IdleExitMinutes = _idleBox.IsChecked == true ? 5 : 0;
            result.StartWithWindows = _working.StartWithWindows;

            Dictionary<string, Category> map = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            foreach (RowEditor ed in _editors)
            {
                string catName = ed.CategoryBox.Text.Trim();
                if (catName.Length == 0) catName = "未分类";

                Category cat;
                if (!map.TryGetValue(catName, out cat))
                {
                    cat = new Category();
                    cat.Name = catName;
                    map[catName] = cat;
                    result.Categories.Add(cat);
                }

                Item it = ed.Item;
                it.Name = ed.NameBox.Text.Trim();
                it.Key = ed.KeyBox.Text.Trim().ToUpperInvariant();
                it.Path = ed.PathBox.Text.Trim();
                cat.Items.Add(it);
            }
            return result;
        }

        private void Save()
        {
            if (!Validate()) return;

            AppConfig result = BuildConfig();
            try
            {
                Program.SaveConfig(result);
                _working = result;
                _dirty = false;
                Close();
            }
            catch (Exception ex)
            {
                SetStatus("保存失败：" + ex.Message, true);
            }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_dirty) return;
            MessageBoxResult r = MessageBox.Show(this,
                "还有未保存的修改，确定关闭吗？",
                "SmartLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) e.Cancel = true;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int dark = Theme.IsDark ? 1 : 0;
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
            int corner = Native.DWMWCP_ROUND;
            Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, 4);
            int noBorder = unchecked((int)0xFFFFFFFE);
            Native.DwmSetWindowAttribute(hwnd, 34, ref noBorder, 4);
        }

        private class RowEditor
        {
            public Item Item;
            public string Category;
            public Border Container;
            public TextBox CategoryBox;
            public TextBox KeyBox;
            public TextBox NameBox;
            public Button TypeButton;
            public TextBox PathBox;
            public TextBlock PathWatermark;
            public Button BrowseButton;
            public Image IconImage;

            public void RefreshIcon()
            {
                IconService.Clear();
                IconImage.Source = IconService.Get(Item);
            }
        }
    }
}
