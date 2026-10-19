using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartLauncher
{
    public partial class PanelWindow
    {        private void BuildRows()
        {
            _hoverRow = null;
            _list.Children.Clear();
            _rows.Clear();

            double contentHeight = 13;
            bool first = true;
            int index = 0;

            if (_config != null && _config.Categories != null)
            {
                foreach (Category cat in _config.Categories)
                {
                    if (cat == null) continue;
                    if (!first)
                    {
                        Border sep = new Border();
                        sep.Height = 1;
                        sep.Background = Theme.SeparatorBrush;
                        sep.Margin = new Thickness(9, 3, 9, 4);
                        _list.Children.Add(sep);
                        contentHeight += 8;
                    }
                    first = false;

                    Border headerBox = new Border();
                    headerBox.Height = 23;
                    headerBox.Padding = new Thickness(9, 0, 0, 0);
                    TextBlock header = new TextBlock();
                    header.Text = cat.Name;
                    header.FontSize = 10.5;
                    header.FontWeight = FontWeights.SemiBold;
                    header.Foreground = Theme.TextSecondaryBrush;
                    header.VerticalAlignment = VerticalAlignment.Center;
                    headerBox.Child = header;
                    _list.Children.Add(headerBox);
                    contentHeight += 23;

                    if (cat.Items == null) continue;
                    foreach (Item item in cat.Items)
                    {
                        if (item == null) continue;
                        RowView row = BuildRow(item, index);
                        index++;
                        _list.Children.Add(row.Container);
                        _rows.Add(row);
                        contentHeight += 29;
                    }
                }
            }

            if (_rows.Count == 0)
            {
                TextBlock empty = new TextBlock();
                empty.Text = "还没有条目，右键打开设置";
                empty.FontSize = 11.5;
                empty.Foreground = Theme.TextSecondaryBrush;
                empty.Margin = new Thickness(12, 14, 12, 14);
                _list.Children.Add(empty);
                contentHeight += 45;
            }

            Height = Math.Min(contentHeight, 720);
            UpdateSelection();
        }

        private RowView BuildRow(Item item, int index)
        {
            RowView row = new RowView();
            row.Item = item;
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
            grid.ColumnDefinitions[2].Width = new GridLength(29);

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
            icon.Width = 20;
            icon.Height = 20;
            icon.Stretch = Stretch.Uniform;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Source = IconService.Get(item);
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
            row.Icon = icon;

            TextBlock name = new TextBlock();
            name.Text = item.Name;
            name.FontSize = 12.2;
            name.Foreground = Theme.TextPrimaryBrush;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            name.Margin = new Thickness(7, 0, 5, 0);
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);
            row.Name = name;

            Border keyBox = new Border();
            keyBox.Width = 20;
            keyBox.Height = 20;
            keyBox.CornerRadius = new CornerRadius(4);
            keyBox.Background = Theme.KeycapBgBrush;
            keyBox.BorderBrush = Theme.KeycapBorderBrush;
            keyBox.BorderThickness = new Thickness(1);
            keyBox.HorizontalAlignment = HorizontalAlignment.Right;
            keyBox.VerticalAlignment = VerticalAlignment.Center;
            keyBox.Margin = new Thickness(0, 0, 7, 0);
            TextBlock key = new TextBlock();
            key.Text = item.Key == null ? "" : item.Key.ToUpperInvariant();
            key.FontSize = 9;
            key.Foreground = Theme.KeyTextBrush;
            key.HorizontalAlignment = HorizontalAlignment.Center;
            key.VerticalAlignment = VerticalAlignment.Center;
            keyBox.Child = key;
            Grid.SetColumn(keyBox, 2);
            grid.Children.Add(keyBox);
            row.KeyBox = keyBox;
            row.Key = key;
            if (string.IsNullOrEmpty(item.Key)) keyBox.Visibility = Visibility.Collapsed;

            container.Child = grid;
            container.Tag = row;
            container.MouseLeftButtonUp += delegate
            {
                Launch(row);
            };
            row.Container = container;
            return row;
        }

        private class RowView
        {
            public Item Item;
            public Border Container;
            public Border KeyBox;
            public TextBlock Key;
            public TextBlock Name;
            public Image Icon;
            public Border Indicator;
            public SolidColorBrush Bg;
            public bool Hover;
            public bool Selected;
            public int Index;

            public void Apply()
            {
                Color target = Selected ? Theme.RowSelectedColor : (Hover ? Theme.RowHoverColor : Colors.Transparent);
                if (!Bg.Color.Equals(target)) Bg.Color = target;

                Name.Foreground = Theme.TextPrimaryBrush;
                Key.Foreground = Theme.KeyTextBrush;
                KeyBox.Background = Theme.KeycapBgBrush;
                KeyBox.BorderBrush = Theme.KeycapBorderBrush;
                Indicator.Visibility = Selected ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
