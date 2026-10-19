using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SmartLauncher
{
    public static class Ui
    {
        public static Style ButtonStyle(bool accent)
        {
            return ButtonStyle(accent, false);
        }

        public static Style ButtonStyle(bool accent, bool danger)
        {
            Style s = new Style(typeof(Button));
            s.Setters.Add(new Setter(Button.BackgroundProperty, accent ? Theme.AccentBrush : Theme.ControlBgBrush));
            s.Setters.Add(new Setter(Button.ForegroundProperty, accent ? Theme.AccentTextBrush : Theme.TextPrimaryBrush));
            s.Setters.Add(new Setter(Button.BorderBrushProperty, accent ? Theme.AccentBrush : Theme.ControlBorderBrush));
            s.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
            s.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(12, 5, 12, 5)));
            s.Setters.Add(new Setter(Button.FontSizeProperty, 12.5));
            s.Setters.Add(new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            s.Setters.Add(new Setter(Button.FontFamilyProperty, Theme.UiFont));
            s.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
            s.Setters.Add(new Setter(Button.MinHeightProperty, 28.0));
            s.Setters.Add(new Setter(Button.FocusVisualStyleProperty, null));
            s.Setters.Add(new Setter(Button.TemplateProperty, ButtonTemplate(accent, danger)));
            return s;
        }

        private static ControlTemplate ButtonTemplate(bool accent, bool danger)
        {
            FrameworkElementFactory root = new FrameworkElementFactory(typeof(Grid));
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            bd.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory inner = new FrameworkElementFactory(typeof(Grid));
            FrameworkElementFactory hv = new FrameworkElementFactory(typeof(Border));
            hv.Name = "hv";
            hv.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            Color hoverColor = danger
                ? Color.FromArgb(0x30, 0xC4, 0x2B, 0x1C)
                : (accent ? Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)
                    : (Theme.IsDark ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x10, 0x00, 0x00, 0x00)));
            hv.SetValue(Border.BackgroundProperty, new SolidColorBrush(hoverColor));
            hv.SetValue(UIElement.OpacityProperty, 0.0);

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetBinding(ContentPresenter.HorizontalAlignmentProperty, new Binding("HorizontalContentAlignment") { RelativeSource = RelativeSource.TemplatedParent });
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            cp.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            cp.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ContentTemplate") { RelativeSource = RelativeSource.TemplatedParent });

            inner.AppendChild(hv);
            inner.AppendChild(cp);
            bd.AppendChild(inner);
            root.AppendChild(bd);

            ControlTemplate ct = new ControlTemplate(typeof(Button));
            ct.VisualTree = root;

            Trigger hover = new Trigger();
            hover.Property = Button.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "hv"));
            ct.Triggers.Add(hover);

            Trigger pressed = new Trigger();
            pressed.Property = Button.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "hv"));
            pressed.Setters.Add(new Setter(UIElement.RenderTransformProperty,
                new ScaleTransform(0.985, 0.985), "bd"));
            ct.Triggers.Add(pressed);

            Trigger disabled = new Trigger();
            disabled.Property = Button.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "bd"));
            ct.Triggers.Add(disabled);

            return ct;
        }

        public static Style TextBoxStyle()
        {
            Style s = new Style(typeof(TextBox));
            s.Setters.Add(new Setter(TextBox.BackgroundProperty, Theme.ControlBgBrush));
            s.Setters.Add(new Setter(TextBox.ForegroundProperty, Theme.TextPrimaryBrush));
            s.Setters.Add(new Setter(TextBox.BorderBrushProperty, Theme.ControlBorderBrush));
            s.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            s.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(8, 4, 8, 4)));
            s.Setters.Add(new Setter(TextBox.FontSizeProperty, 12.5));
            s.Setters.Add(new Setter(TextBox.FontFamilyProperty, Theme.UiFont));
            s.Setters.Add(new Setter(TextBox.CaretBrushProperty, Theme.TextPrimaryBrush));
            s.Setters.Add(new Setter(TextBox.SelectionBrushProperty, Theme.AccentBrush));
            s.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            s.Setters.Add(new Setter(TextBox.MinHeightProperty, 28.0));
            s.Setters.Add(new Setter(TextBox.FocusVisualStyleProperty, null));
            s.Setters.Add(new Setter(TextBox.TemplateProperty, TextBoxTemplate()));
            return s;
        }

        private static ControlTemplate TextBoxTemplate()
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            bd.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
            bd.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });

            FrameworkElementFactory sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.Name = "PART_ContentHost";
            sv.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            bd.AppendChild(sv);

            ControlTemplate ct = new ControlTemplate(typeof(TextBox));
            ct.VisualTree = bd;

            Trigger focus = new Trigger();
            focus.Property = TextBox.IsKeyboardFocusedProperty;
            focus.Value = true;
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.AccentBrush, "bd"));
            ct.Triggers.Add(focus);

            Trigger hover = new Trigger();
            hover.Property = TextBox.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BorderBrushProperty,
                new SolidColorBrush(Theme.IsDark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x30, 0x00, 0x00, 0x00)), "bd"));
            ct.Triggers.Add(hover);

            return ct;
        }

        public static Style ScrollBarStyle()
        {
            string color = Theme.IsDark ? "#55FFFFFF" : "#55000000";
            string xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Grid Width='10' Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border CornerRadius='4' Margin='2,0' Background='" + color + "'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>";
            ControlTemplate ct = (ControlTemplate)XamlReader.Parse(xaml);
            Style s = new Style(typeof(ScrollBar));
            s.Setters.Add(new Setter(ScrollBar.WidthProperty, 10.0));
            s.Setters.Add(new Setter(ScrollBar.BackgroundProperty, Brushes.Transparent));
            s.Setters.Add(new Setter(ScrollBar.TemplateProperty, ct));
            s.Setters.Add(new Setter(ScrollBar.FocusVisualStyleProperty, null));
            return s;
        }
        public static Style CheckBoxStyle()
        {
            Style s = new Style(typeof(CheckBox));
            s.Setters.Add(new Setter(CheckBox.ForegroundProperty, Theme.TextPrimaryBrush));
            s.Setters.Add(new Setter(CheckBox.FontSizeProperty, 12.5));
            s.Setters.Add(new Setter(CheckBox.FontFamilyProperty, Theme.UiFont));
            s.Setters.Add(new Setter(CheckBox.CursorProperty, System.Windows.Input.Cursors.Hand));
            s.Setters.Add(new Setter(CheckBox.FocusVisualStyleProperty, null));
            s.Setters.Add(new Setter(CheckBox.TemplateProperty, CheckBoxTemplate()));
            return s;
        }

        private static ControlTemplate CheckBoxTemplate()
        {
            FrameworkElementFactory root = new FrameworkElementFactory(typeof(Grid));
            root.SetValue(FrameworkElement.MinHeightProperty, 24.0);
            FrameworkElementFactory c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(18));
            FrameworkElementFactory c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            root.AppendChild(c0);
            root.AppendChild(c1);

            FrameworkElementFactory box = new FrameworkElementFactory(typeof(Border));
            box.Name = "box";
            box.SetValue(Border.WidthProperty, 18.0);
            box.SetValue(Border.HeightProperty, 18.0);
            box.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            box.SetValue(Border.BackgroundProperty, Theme.ControlBgBrush);
            box.SetValue(Border.BorderBrushProperty, Theme.ControlBorderBrush);
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            box.SetValue(Grid.ColumnProperty, 0);

            FrameworkElementFactory check = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            check.Name = "check";
            check.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 3,9 L 7,13 L 15,4"));
            check.SetValue(System.Windows.Shapes.Path.StrokeProperty, Brushes.White);
            check.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 2.0);
            check.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round);
            check.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round);
            check.SetValue(System.Windows.Shapes.Path.StrokeLineJoinProperty, PenLineJoin.Round);
            check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            box.AppendChild(check);
            root.AppendChild(box);

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(Grid.ColumnProperty, 1);
            cp.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
            root.AppendChild(cp);

            ControlTemplate ct = new ControlTemplate(typeof(CheckBox));
            ct.VisualTree = root;

            Trigger on = new Trigger();
            on.Property = CheckBox.IsCheckedProperty;
            on.Value = true;
            on.Setters.Add(new Setter(Border.BackgroundProperty, Theme.AccentBrush, "box"));
            on.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.AccentBrush, "box"));
            on.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "check"));
            ct.Triggers.Add(on);

            Trigger hover = new Trigger();
            hover.Property = CheckBox.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.AccentBrush, "box"));
            ct.Triggers.Add(hover);

            Trigger disabled = new Trigger();
            disabled.Property = CheckBox.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4, "box"));
            ct.Triggers.Add(disabled);

            return ct;
        }

        public static DropShadowEffect Shadow()
        {
            DropShadowEffect e = new DropShadowEffect();
            e.BlurRadius = 20;
            e.ShadowDepth = 0;
            e.Opacity = 0.35;
            e.Color = Colors.Black;
            return e;
        }

        public static TextBlock Label(string text, double size, Brush brush)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = size;
            t.Foreground = brush;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.FontFamily = Theme.UiFont;
            return t;
        }
    }
}
