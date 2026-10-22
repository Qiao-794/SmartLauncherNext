using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartLauncher
{
    public partial class PanelWindow
    {
        private const int MaxWindowRows = 8;

        private void BuildWindowRows()
        {
            SyncCursorPoint();
            _list.Children.Clear();
            _rows.Clear();

            uint selfPid = (uint)Process.GetCurrentProcess().Id;
            IntPtr foreground = Native.GetForegroundWindow();
            int total;
            List<WindowInfo> windows = WindowList.Enumerate(selfPid, MaxWindowRows, out total);
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

            double contentHeight = 13;
            if (windows.Count == 0)
            {
                TextBlock empty = new TextBlock();
                empty.Text = "没有其他窗口";
                empty.FontSize = 11.5;
                empty.Foreground = Theme.TextSecondaryBrush;
                empty.Margin = new Thickness(12, 14, 12, 14);
                _list.Children.Add(empty);
                contentHeight += 45;
            }
            else
            {
                int index = 0;
                foreach (WindowInfo info in windows)
                {
                    RowView row = BuildWindowRow(info, index, info.Hwnd == foreground);
                    index++;
                    _list.Children.Add(row.Container);
                    _rows.Add(row);
                    contentHeight += 29;
                }

                if (total > windows.Count)
                {
                    TextBlock more = new TextBlock();
                    more.Text = "还有 " + (total - windows.Count) + " 个窗口";
                    more.FontSize = 9.5;
                    more.Foreground = Theme.TextSecondaryBrush;
                    more.Margin = new Thickness(12, 3, 12, 3);
                    _list.Children.Add(more);
                    contentHeight += 19;
                }
            }

            Height = contentHeight + ShadowMargin * 2;
            _selected = _rows.Count > 1 ? 1 : (_rows.Count == 1 ? 0 : -1);
            UpdateSelection();
        }

        private RowView BuildWindowRow(WindowInfo info, int index, bool isForeground)
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
            icon.Source = IconService.GetForWindow(info.Hwnd, info.ExePath, info.Title);
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
            if (WindowList.Activate(hwnd))
            {
                HidePanel(false);
            }
            else
            {
                ShowStatus("无法切换到该窗口");
            }
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
