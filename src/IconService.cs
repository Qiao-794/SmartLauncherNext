using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace SmartLauncher
{
    public static class IconService
    {
        private static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();
        private static List<string> _shortcuts;
        private static readonly Color[] Palette = new Color[]
        {
            Color.FromRgb(0x00, 0x78, 0xD4),
            Color.FromRgb(0x00, 0xB2, 0x94),
            Color.FromRgb(0x87, 0x64, 0xB8),
            Color.FromRgb(0xCA, 0x50, 0x10),
            Color.FromRgb(0xC2, 0x39, 0xB3),
            Color.FromRgb(0x10, 0x7C, 0x10),
            Color.FromRgb(0xD1, 0x34, 0x38),
            Color.FromRgb(0x5D, 0x5A, 0x58)
        };

        public static void Clear()
        {
            Cache.Clear();
        }

        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int ICON_SMALL2 = 2;
        private const int GCLP_HICON = -14;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int index);

        /// <summary>
        /// Icon of a live window: window icon first (works for UWP frames), then the
        /// owner process image, then a generated letter tile.
        /// </summary>
        public static BitmapSource GetForWindow(IntPtr hwnd, string exePath, string title, bool cheap)
        {
            string key = "win|" + hwnd.ToInt64().ToString("x") + "|" + (exePath ?? "");
            BitmapSource cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            BitmapSource img = null;
            if (!cheap)
            {
                try
                {
                    IntPtr result;
                    IntPtr hIcon = IntPtr.Zero;
                    if (SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero, SMTO_ABORTIFHUNG, 60, out result) != IntPtr.Zero)
                        hIcon = result;
                    if (hIcon == IntPtr.Zero && SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero, SMTO_ABORTIFHUNG, 60, out result) != IntPtr.Zero)
                        hIcon = result;
                    if (hIcon == IntPtr.Zero)
                        hIcon = GetClassLongPtr(hwnd, GCLP_HICON);

                    if (hIcon != IntPtr.Zero)
                    {
                        BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bmp.Freeze();
                        img = bmp;
                    }
                }
                catch
                {
                }
            }

            if (img == null && !string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                img = FromExe(exePath);

            if (img == null)
                img = Letter(string.IsNullOrEmpty(title) ? "?" : title);

            Cache[key] = img;
            return img;
        }

        public static BitmapSource Get(Item item)
        {
            if (item == null) return Letter("?");

            string iconPath = item.IconPath == null ? "" : item.IconPath.Trim();
            string resolved = iconPath;
            if (resolved.Length > 0 && !Path.IsPathRooted(resolved))
                resolved = Path.Combine(ConfigStore.BaseDir, resolved);

            long stamp = 0;
            try
            {
                string stampPath = File.Exists(resolved) ? resolved : (item.Type == "exe" && File.Exists(item.Path) ? item.Path : null);
                if (stampPath != null) stamp = File.GetLastWriteTimeUtc(stampPath).Ticks;
            }
            catch { }
            string key = resolved + "|" + stamp + "|" + item.Type + "|" + item.Path + "|" + item.Name;
            BitmapSource cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            BitmapSource img = null;
            if (resolved.Length > 0 && File.Exists(resolved))
                img = FromImageFile(resolved);

            if (img == null && item.Type == "exe" && !string.IsNullOrEmpty(item.Path) && File.Exists(item.Path))
                img = FromExe(item.Path);

            if (img == null && !string.IsNullOrEmpty(item.Path) && File.Exists(item.Path))
                img = FromExe(item.Path);

            if (img == null && !string.IsNullOrEmpty(item.Name))
            {
                string lnk = FindShortcut(item.Name);
                if (lnk != null) img = FromShortcut(lnk);
            }

            if (img == null && !string.IsNullOrEmpty(item.Name))
            {
                string exe = FindAppPath(item.Name);
                if (exe != null) img = FromExe(exe);
            }

            if (img == null)
                img = Letter(string.IsNullOrEmpty(item.Name) ? "?" : item.Name);

            Cache[key] = img;
            return img;
        }

        private static BitmapSource FromImageFile(string path)
        {
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource FromExe(string path)
        {
            IntPtr[] icons = new IntPtr[1];
            uint[] ids = new uint[1];
            try
            {
                uint n = Native.PrivateExtractIcons(path, 0, 64, 64, icons, ids, 1, 0);
                if (n > 0 && icons[0] != IntPtr.Zero)
                {
                    BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(
                        icons[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
            }
            finally
            {
                if (icons[0] != IntPtr.Zero) Native.DestroyIcon(icons[0]);
            }

            try
            {
                using (System.Drawing.Icon ico = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (ico != null)
                    {
                        BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(
                            ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static BitmapSource FromShortcut(string lnk)
        {
            Native.SHFILEINFO info = new Native.SHFILEINFO();
            IntPtr r = IntPtr.Zero;
            try
            {
                r = Native.SHGetFileInfo(lnk, 0, ref info,
                    (uint)Marshal.SizeOf(typeof(Native.SHFILEINFO)), 0x100);
                if (r != IntPtr.Zero && info.hIcon != IntPtr.Zero)
                {
                    BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
            }
            finally
            {
                if (info.hIcon != IntPtr.Zero) Native.DestroyIcon(info.hIcon);
            }
            return null;
        }

        private static string FindShortcut(string name)
        {
            if (_shortcuts == null)
            {
                _shortcuts = new List<string>();
                string[] roots = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
                };
                foreach (string root in roots)
                {
                    try
                    {
                        if (Directory.Exists(root))
                            _shortcuts.AddRange(Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories));
                    }
                    catch
                    {
                    }
                }
            }

            string target = Norm(name);
            if (target.Length == 0) return null;

            foreach (string s in _shortcuts)
            {
                if (Norm(Path.GetFileNameWithoutExtension(s)) == target) return s;
            }
            if (target.Length >= 3)
            {
                foreach (string s in _shortcuts)
                {
                    string fn = Norm(Path.GetFileNameWithoutExtension(s));
                    if (fn.EndsWith(target) || fn.StartsWith(target)) return s;
                }
            }
            return null;
        }

        private static string FindAppPath(string name)
        {
            string[] keys = new string[] { name + ".exe", name };
            foreach (string key in keys)
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + key))
                    {
                        if (k == null) continue;
                        string p = k.GetValue("") as string;
                        if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s.ToLowerInvariant();
            t = t.Replace(" ", "").Replace("-", "").Replace("_", "").Replace(".", "");
            return t;
        }

        private static BitmapSource Letter(string name)
        {
            const int size = 64;
            int h = name.GetHashCode() & 0x7FFFFFFF;
            Color bg = Palette[h % Palette.Length];

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                SolidColorBrush brush = new SolidColorBrush(bg);
                brush.Freeze();
                dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, size, size), 15, 15);

                string ch = name.Substring(0, 1).ToUpperInvariant();
                Typeface tf = new Typeface(Theme.UiFont, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                FormattedText ft = new FormattedText(ch, CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, tf, 34, Brushes.White, 1.0);
                dc.DrawText(ft, new Point((size - ft.Width) / 2.0, (size - ft.Height) / 2.0 - 2));
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
    }
}
