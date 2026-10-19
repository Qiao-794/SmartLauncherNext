using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace SmartLauncher
{
    public partial class SettingsWindow
    {        private UIElement BuildTitleBar()
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });

            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;
            left.Margin = new Thickness(16, 0, 0, 0);

            Border icon = new Border();
            icon.Width = 22;
            icon.Height = 22;
            icon.CornerRadius = new CornerRadius(5);
            icon.Background = Theme.AccentBrush;
            icon.Margin = new Thickness(0, 0, 10, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            TextBlock iconText = new TextBlock();
            iconText.Text = "S";
            iconText.FontSize = 12;
            iconText.FontWeight = FontWeights.Bold;
            iconText.Foreground = Brushes.White;
            iconText.HorizontalAlignment = HorizontalAlignment.Center;
            iconText.VerticalAlignment = VerticalAlignment.Center;
            icon.Child = iconText;
            left.Children.Add(icon);

            TextBlock title = new TextBlock();
            title.Text = "SmartLauncher 设置";
            title.FontSize = 13.5;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Theme.TextPrimaryBrush;
            title.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(title);

            TextBlock sub = new TextBlock();
            sub.Text = "启动面板";
            sub.FontSize = 11.5;
            sub.Foreground = Theme.TextSecondaryBrush;
            sub.VerticalAlignment = VerticalAlignment.Center;
            sub.Margin = new Thickness(10, 1, 0, 0);
            left.Children.Add(sub);

            TextBlock version = new TextBlock();
            version.Text = "v" + typeof(SettingsWindow).Assembly.GetName().Version.ToString(2);
            version.FontSize = 10.5;
            version.Foreground = Theme.TextSecondaryBrush;
            version.VerticalAlignment = VerticalAlignment.Center;
            version.Margin = new Thickness(8, 2, 0, 0);
            left.Children.Add(version);
            g.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Top;
            Button min = WindowButton("—", false);
            min.Click += delegate { WindowState = WindowState.Minimized; };
            Button close = WindowButton("✕", true);
            close.Click += delegate { Close(); };
            right.Children.Add(min);
            right.Children.Add(close);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            return g;
        }

        private Button WindowButton(string glyph, bool danger)
        {
            Button b = new Button();
            b.Content = glyph;
            b.Width = 46;
            b.Height = 32;
            b.FontSize = 12;
            b.Style = Ui.ButtonStyle(false, danger);
            b.Padding = new Thickness(0);
            b.BorderThickness = new Thickness(0);
            b.Background = Brushes.Transparent;
            b.Foreground = Theme.TextPrimaryBrush;
            b.Focusable = false;
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        private UIElement BuildToolbar()
        {
            Grid g = new Grid();
            g.Margin = new Thickness(16, 0, 16, 0);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;

            Button add = TextButton("＋  添加行", true);
            add.Click += delegate { AddRow(); };
            left.Children.Add(add);

            Button import = TextButton("导入旧配置", false);
            import.Click += delegate { ImportLegacy(); };
            left.Children.Add(import);

            Button open = TextButton("打开数据目录", false);
            open.Click += delegate { OpenDataDir(); };
            left.Children.Add(open);

            TextBlock filterLabel = new TextBlock();
            filterLabel.Text = "筛选";
            filterLabel.FontSize = 11.5;
            filterLabel.Foreground = Theme.TextSecondaryBrush;
            filterLabel.VerticalAlignment = VerticalAlignment.Center;
            filterLabel.Margin = new Thickness(2, 0, 6, 0);
            left.Children.Add(filterLabel);

            _filterBox = new TextBox();
            _filterBox.Width = 150;
            _filterBox.Height = 28;
            _filterBox.Style = Ui.TextBoxStyle();
            _filterBox.FontSize = 12;
            _filterBox.Padding = new Thickness(8, 0, 8, 0);
            _filterBox.ToolTip = "按名称 / 分类 / 路径筛选";
            _filterBox.TextChanged += delegate { ApplyFilter(_filterBox.Text); };
            left.Children.Add(_filterBox);

            g.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            Button up = TextButton("↑", false);
            up.Width = 30;
            up.Padding = new Thickness(0);
            up.FontSize = 13;
            up.ToolTip = "上移当前行";
            up.Click += delegate { MoveEditor(-1); };
            right.Children.Add(up);

            Button down = TextButton("↓", false);
            down.Width = 30;
            down.Padding = new Thickness(0);
            down.FontSize = 13;
            down.ToolTip = "下移当前行";
            down.Click += delegate { MoveEditor(1); };
            right.Children.Add(down);

            Button revert = TextButton("还原", false);
            revert.Margin = new Thickness(0);
            revert.Click += delegate { Revert(); };
            right.Children.Add(revert);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            return g;
        }

        private Button TextButton(string text, bool accent)
        {
            Button b = new Button();
            b.Content = text;
            b.Style = Ui.ButtonStyle(accent);
            b.FontSize = 12;
            b.Height = 30;
            b.Padding = new Thickness(12, 0, 12, 0);
            b.Margin = new Thickness(0, 0, 8, 0);
            b.VerticalAlignment = VerticalAlignment.Center;
            return b;
        }

        private UIElement BuildHeader()
        {
            Border border = new Border();
            border.BorderBrush = Theme.SeparatorBrush;
            border.BorderThickness = new Thickness(0, 0, 0, 1);
            border.Margin = new Thickness(16, 0, 26, 0);
            border.Background = Brushes.Transparent;

            Grid g = ColumnsGrid();
            string[] labels = new string[] { "分类", "快捷键", "名称", "类型", "路径 / 动作 ID", "图标", "操作" };
            for (int i = 0; i < labels.Length; i++)
            {
                TextBlock t = new TextBlock();
                t.Text = labels[i];
                t.FontSize = 11.5;
                t.Foreground = Theme.TextSecondaryBrush;
                t.VerticalAlignment = VerticalAlignment.Center;
                t.Margin = new Thickness(10, 0, 8, 0);
                Grid.SetColumn(t, i);
                g.Children.Add(t);
            }
            border.Child = g;
            return border;
        }

        private UIElement BuildFooter()
        {
            Border border = new Border();
            border.BorderBrush = Theme.SeparatorBrush;
            border.BorderThickness = new Thickness(0, 1, 0, 0);
            border.Margin = new Thickness(16, 0, 16, 0);
            border.Background = Brushes.Transparent;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(_countText, 0);
            g.Children.Add(_countText);
            Grid.SetColumn(_idleBox, 1);
            g.Children.Add(_idleBox);
            Grid.SetColumn(_statusText, 3);
            g.Children.Add(_statusText);

            Button cancel = TextButton("取消", false);
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { Close(); };
            Grid.SetColumn(cancel, 4);
            g.Children.Add(cancel);

            Button save = TextButton("保存", true);
            save.Margin = new Thickness(0);
            save.Click += delegate { Save(); };
            Grid.SetColumn(save, 5);
            g.Children.Add(save);

            border.Child = g;
            return border;
        }

        private Grid ColumnsGrid()
        {
            Grid g = new Grid();
            foreach (GridLength w in Cols)
            {
                ColumnDefinition c = new ColumnDefinition();
                c.Width = w;
                g.ColumnDefinitions.Add(c);
            }
            return g;
        }

        private static void Put(Grid g, FrameworkElement element, int column)
        {
            element.Margin = new Thickness(0, 0, 8, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(element, column);
            g.Children.Add(element);
        }
    }
}
