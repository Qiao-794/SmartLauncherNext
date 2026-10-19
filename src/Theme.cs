using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace SmartLauncher
{
    public static class Theme
    {
        public static bool IsDark;
        public static int OverrideTheme = -1;
        public static Color Accent;
        public static readonly FontFamily UiFont =
            new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");

        public static SolidColorBrush PanelTintBrush;
        public static SolidColorBrush PanelSolidBrush;
        public static SolidColorBrush PanelBorderBrush;
        public static SolidColorBrush TextPrimaryBrush;
        public static SolidColorBrush TextSecondaryBrush;
        public static SolidColorBrush RowHoverBrush;
        public static SolidColorBrush RowSelectedBrush;
        public static SolidColorBrush KeycapBgBrush;
        public static SolidColorBrush KeycapBorderBrush;
        public static SolidColorBrush KeyTextBrush;
        public static SolidColorBrush SeparatorBrush;
        public static SolidColorBrush ControlBgBrush;
        public static SolidColorBrush ControlBorderBrush;
        public static SolidColorBrush WindowTintBrush;
        public static SolidColorBrush CardBgBrush;
        public static SolidColorBrush CardBorderBrush;
        public static SolidColorBrush MenuBgBrush;
        public static SolidColorBrush AccentBrush;
        public static SolidColorBrush AccentTextBrush;
        public static SolidColorBrush StatusBgBrush;
        public static SolidColorBrush FallbackBgBrush;
        public static SolidColorBrush WindowFgBrush;
        public static SolidColorBrush WindowSolidBrush;
        public static SolidColorBrush CardSolidBrush;
        public static SolidColorBrush CardSolidBorder;
        public static SolidColorBrush RowAltBrush;
        public static SolidColorBrush ScrollThumbBrush;

        public static Color RowHoverColor;
        public static Color RowSelectedColor;

        private static bool _loaded;

        public static bool Load()
        {
            bool dark = ReadDark();
            Color accent = ReadAccent();
            if (_loaded && dark == IsDark && accent == Accent)
                return false;

            IsDark = dark;
            Accent = accent;
            BuildBrushes();
            _loaded = true;
            return true;
        }

        private static bool ReadDark()
        {
            if (OverrideTheme >= 0) return OverrideTheme == 1;
            int light = 1;
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                if (v is int) light = (int)v;
            }
            catch
            {
            }
            return light == 0;
        }

        private static Color ReadAccent()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null);
                if (v is int)
                {
                    int n = (int)v;
                    byte r = (byte)(n & 0xFF);
                    byte g = (byte)((n >> 8) & 0xFF);
                    byte b = (byte)((n >> 16) & 0xFF);
                    if ((int)r + g + b > 24)
                        return Color.FromRgb(r, g, b);
                }
            }
            catch
            {
            }
            return Color.FromRgb(0x00, 0x78, 0xD4);
        }

        private static void BuildBrushes()
        {
            if (IsDark)
            {
                PanelTintBrush = B(Color.FromArgb(0xBF, 0x1A, 0x1A, 0x1A));
                PanelSolidBrush = B(Color.FromRgb(0x1F, 0x1F, 0x1F));
                PanelBorderBrush = B(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
                TextPrimaryBrush = B(Color.FromArgb(0xF5, 0xFF, 0xFF, 0xFF));
                TextSecondaryBrush = B(Color.FromArgb(0x8F, 0xFF, 0xFF, 0xFF));
                RowHoverColor = Color.FromArgb(0x2E, Accent.R, Accent.G, Accent.B);
                RowSelectedColor = Color.FromArgb(0x3A, Accent.R, Accent.G, Accent.B);
                RowHoverBrush = B(RowHoverColor);
                RowSelectedBrush = B(RowSelectedColor);
                KeycapBgBrush = B(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
                KeycapBorderBrush = B(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
                KeyTextBrush = B(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));
                SeparatorBrush = B(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
                ControlBgBrush = B(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
                ControlBorderBrush = B(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF));
                WindowTintBrush = B(Color.FromArgb(0x8C, 0x00, 0x00, 0x00));
                CardBgBrush = B(Color.FromArgb(0x38, 0x00, 0x00, 0x00));
                CardBorderBrush = B(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
                MenuBgBrush = B(Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B));
                StatusBgBrush = B(Color.FromArgb(0xE6, 0xC4, 0x2B, 0x1C));
                FallbackBgBrush = B(Color.FromArgb(0xF2, 0x20, 0x20, 0x20));
                WindowFgBrush = TextPrimaryBrush;
                WindowSolidBrush = B(Color.FromRgb(0x1C, 0x1C, 0x1C));
                CardSolidBrush = B(Color.FromRgb(0x25, 0x25, 0x25));
                CardSolidBorder = B(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
                RowAltBrush = B(Color.FromArgb(0x08, 0xFF, 0xFF, 0xFF));
                ScrollThumbBrush = B(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
                AccentTextBrush = B(Colors.White);
            }
            else
            {
                PanelTintBrush = B(Color.FromArgb(0xF2, 0xF7, 0xF7, 0xF7));
                PanelSolidBrush = B(Color.FromRgb(0xF7, 0xF7, 0xF7));
                PanelBorderBrush = B(Color.FromRgb(0xE5, 0xE5, 0xE5));
                TextPrimaryBrush = B(Color.FromArgb(0xE4, 0x00, 0x00, 0x00));
                TextSecondaryBrush = B(Color.FromArgb(0x73, 0x00, 0x00, 0x00));
                RowHoverColor = Color.FromArgb(0x1F, Accent.R, Accent.G, Accent.B);
                RowSelectedColor = Color.FromArgb(0x30, Accent.R, Accent.G, Accent.B);
                RowHoverBrush = B(RowHoverColor);
                RowSelectedBrush = B(RowSelectedColor);
                KeycapBgBrush = B(Color.FromArgb(0x08, 0x00, 0x00, 0x00));
                KeycapBorderBrush = B(Color.FromArgb(0x14, 0x00, 0x00, 0x00));
                KeyTextBrush = B(Color.FromArgb(0x99, 0x00, 0x00, 0x00));
                SeparatorBrush = B(Color.FromArgb(0x0F, 0x00, 0x00, 0x00));
                ControlBgBrush = B(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
                ControlBorderBrush = B(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
                WindowTintBrush = B(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
                CardBgBrush = B(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF));
                CardBorderBrush = B(Color.FromArgb(0x10, 0x00, 0x00, 0x00));
                MenuBgBrush = B(Color.FromArgb(0xF2, 0xF9, 0xF9, 0xF9));
                StatusBgBrush = B(Color.FromArgb(0xE6, 0xC4, 0x2B, 0x1C));
                FallbackBgBrush = B(Color.FromArgb(0xF2, 0xF3, 0xF3, 0xF3));
                WindowFgBrush = TextPrimaryBrush;
                WindowSolidBrush = B(Color.FromRgb(0xF3, 0xF3, 0xF3));
                CardSolidBrush = B(Color.FromRgb(0xFF, 0xFF, 0xFF));
                CardSolidBorder = B(Color.FromArgb(0x14, 0x00, 0x00, 0x00));
                RowAltBrush = B(Color.FromArgb(0x06, 0x00, 0x00, 0x00));
                ScrollThumbBrush = B(Color.FromArgb(0x55, 0x00, 0x00, 0x00));
                AccentTextBrush = B(Colors.White);
            }

            AccentBrush = B(Accent);
        }

        private static SolidColorBrush B(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
