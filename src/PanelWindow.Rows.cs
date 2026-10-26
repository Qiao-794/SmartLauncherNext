using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartLauncher
{
    public partial class PanelWindow
    {
        private const int MaxVisibleRows = 14;
        private const int MaxEnumRows = 60;

        private void BuildWindowRows()
        {
            SyncCursorPoint();
            _list.Children.Clear();
            _rows.Clear();

            uint selfPid = (uint)Process.GetCurrentProcess().Id;
            IntPtr foreground = Native.GetForegroundWindow();
            int total;
            Stopwatch sw = Stopwatch.StartNew();
            List<WindowInfo> windows = WindowList.Enumerate(selfPid, MaxEnumRows, out total);
            Log.Write("windows rows=" + windows.Count + " total=" + total);
            Log.Write("windows filter: " + WindowList.LastDiagnostics);
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (WindowInfo w in windows)
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(w.Title);
                }
                Log.Write("windows list: " + sb.ToString());
            }
            catch
            {
            }

            int fit = FitVisibleRows();
            int visible = Math.Min(windows.Count, Math.Min(MaxVisibleRows, fit));

            double contentHeight = 13;
            if (windows.Count == 0)
            {
                TextBlock empty = new TextBlock();
                empty.Text = "没有其他窗口";
                empty.FontSize = 11.5;
                empty.Foreground = Theme.TextSecondaryBrush;
                empty.Margin = new Thickness(12, 14, 12, 6);
                _list.Children.Add(empty);
                contentHeight += 39;

                if (_keyMap.Count > 0)
                {
                    TextBlock hint = new TextBlock();
                    hint.Text = "按字母键可直接启动配置项";
                    hint.FontSize = 9.5;
                    hint.Foreground = Theme.TextSecondaryBrush;
                    hint.Margin = new Thickness(12, 0, 12, 14);
                    _list.Children.Add(hint);
                    contentHeight += 20;
                }
            }
            else
            {
                int index = 0;
                foreach (WindowInfo info in windows)
                {
                    bool cheapIcon = index >= 20 || sw.ElapsedMilliseconds > 250;
                    RowView row = BuildWindowRow(info, index, info.Hwnd == foreground, cheapIcon);
                    index++;
                    _list.Children.Add(row.Container);
                    _rows.Add(row);
                }
                contentHeight += 29 * visible;
            }

            Height = contentHeight + ShadowMargin * 2;
            Log.Write("windows built rows=" + _rows.Count + " visible=" + visible + " fit=" + fit
                + " height=" + Height + " in " + sw.ElapsedMilliseconds + "ms");
            _selected = _rows.Count > 1 ? 1 : (_rows.Count == 1 ? 0 : -1);
            UpdateSelection();
        }

        /// <summary>
        /// 面板最多能显示多少行：受 14 行上限和光标所在显示器的工作区高度双重限制。
        /// </summary>
        private int FitVisibleRows()
        {
            double workHeight = 800;
            try
            {
                Native.POINT pt;
                if (Native.GetCursorPos(out pt))
                {
                    IntPtr mon = Native.MonitorFromPoint(pt, 2);
                    Native.MONITORINFO mi = new Native.MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
                    if (mon != IntPtr.Zero && Native.GetMonitorInfo(mon, ref mi))
                    {
                        double scale = 1.0;
                        uint dpiX, dpiY;
                        if (Native.GetDpiForMonitor(mon, Native.MDT_EFFECTIVE_DPI, out dpiX, out dpiY) == 0 && dpiX > 0)
                            scale = dpiX / 96.0;
                        workHeight = (mi.rcWork.Bottom - mi.rcWork.Top) / scale;
                    }
                }
            }
            catch
            {
            }

            double available = workHeight - 30 - ShadowMargin * 2 - 13;
            int rows = (int)Math.Floor(available / 29.0);
            if (rows < 4) rows = 4;
            if (rows > MaxVisibleRows) rows = MaxVisibleRows;
            return rows;
        }

        private RowView BuildWindowRow(WindowInfo info, int index, bool isForeground, bool cheapIcon)
        {
            RowView row = new RowView();
            row.Hwnd = info.Hwnd;
            row.Index = index;
            row.Bg = new SolidColorBrush(Colors.Transparent);

            Border container = new Border();
            container.Height = 29;
            container.CornerRadius = new CornerRadius(4.5);
            container.Background = row.Bg;
            container.Cursor = Cursors.Hand;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions[0].Width = new GridLength(29);
            grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            grid.ColumnDefinitions[2].Width = GridLength.Auto;

            Border indicator = new Border();
            indicator.Width = 2;
            indicator.Height = 13.5;
            indicator.CornerRadius = new CornerRadius(1);
            indicator.Background = Theme.AccentBrush;
            indicator.HorizontalAlignment = HorizontalAlignment.Left;
            indicator.VerticalAlignment = VerticalAlignment.Center;
            indicator.Margin = new Thickness(2, 0, 0, 0);
            indicator.Visibility = Visibility.Collapsed;
            Grid.SetColumn(indicator, 0);
            grid.Children.Add(indicator);
            row.Indicator = indicator;

            Image icon = new Image();
            icon.Width = 18;
            icon.Height = 18;
            icon.Stretch = Stretch.Uniform;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Source = IconService.GetForWindow(info.Hwnd, info.ExePath, info.Title, cheapIcon);
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
            row.Icon = icon;

            TextBlock name = new TextBlock();
            name.Text = info.Title;
            name.FontSize = 12.2;
            name.Foreground = Theme.TextPrimaryBrush;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            name.Margin = new Thickness(7, 0, 6, 0);
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);
            row.Name = name;

            TextBlock meta = new TextBlock();
            meta.Text = isForeground ? "当前" : (info.Minimized ? "最小化" : ShortName(info.ProcessName));
            meta.FontSize = 9;
            meta.Foreground = isForeground ? Theme.AccentBrush : Theme.TextSecondaryBrush;
            meta.VerticalAlignment = VerticalAlignment.Center;
            meta.Margin = new Thickness(0, 0, 9, 0);
            meta.TextTrimming = TextTrimming.CharacterEllipsis;
            meta.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(meta, 2);
            grid.Children.Add(meta);
            row.Meta = meta;

            container.Child = grid;
            container.Tag = row;
            container.MouseLeftButtonUp += delegate
            {
                ActivateWindow(row);
            };

            row.Container = container;
            return row;
        }

        private static string ShortName(string processName)
        {
            string name = (processName ?? "").Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            if (name.Length > 14) name = name.Substring(0, 14);
            return name;
        }

        private void ActivateWindow(RowView row)
        {
            if (row == null || row.Hwnd == IntPtr.Zero) return;

            // 必须仍在前台时切换，否则 SetForegroundWindow 会被系统拒绝
            IntPtr hwnd = row.Hwnd;

            if (!WindowList.Exists(hwnd))
            {
                ShowStatus("该窗口已关闭");
                RefreshRowsInPlace();
                return;
            }

            if (WindowList.Activate(hwnd))
            {
                HidePanel(false);
                return;
            }

            // 切换失败后再确认一次：窗口没了就刷新列表，还活着多半是权限问题
            if (!WindowList.Exists(hwnd))
            {
                ShowStatus("该窗口已关闭");
                RefreshRowsInPlace();
                return;
            }
            ShowStatus("无法切换（可能需要管理员权限）");
        }

        private void RefreshRowsInPlace()
        {
            BuildWindowRows();
            UpdateLayout();
            UpdateFadeState();
        }

        private class RowView
        {
            public IntPtr Hwnd;
            public Border Container;
            public TextBlock Name;
            public TextBlock Meta;
            public Image Icon;
            public Border Indicator;
            public SolidColorBrush Bg;
            public bool Selected;
            public int Index;

            public void Apply()
            {
                Color target = Selected ? Theme.RowSelectedColor : Colors.Transparent;
                if (!Bg.Color.Equals(target)) Bg.Color = target;

                Name.Foreground = Theme.TextPrimaryBrush;
                Indicator.Visibility = Selected ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
