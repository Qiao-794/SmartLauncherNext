using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartLauncher
{
    internal class WindowInfo
    {
        public IntPtr Hwnd;
        public string Title = "";
        public string ProcessName = "";
        public string ExePath = "";
        public uint Pid;
        public bool Minimized;
        public bool TopMost;
    }

    /// <summary>
    /// Top-level window enumeration. Rules follow the well tested predicate used by
    /// PowerToys' WindowWalker (MIT): visible, unowned, not a tool window, not cloaked.
    /// </summary>
    internal static class WindowList
    {
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const uint GW_OWNER = 4;
        private const int DWMWA_CLOAKED = 14;
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int SW_MAXIMIZE = 3;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;
        private const uint WM_CLOSE = 0x0010;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        private delegate bool EnumMonitorsProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, EnumMonitorsProc callback, IntPtr data);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);

        private static readonly Dictionary<uint, string[]> ProcessCache = new Dictionary<uint, string[]>();

        public static string LastDiagnostics = "";

        private static int _skipTool;
        private static int _skipOwned;
        private static int _skipCloaked;
        private static int _skipTitle;

        public static List<WindowInfo> Enumerate(uint selfPid, int max, out int total)
        {
            List<IntPtr> handles = new List<IntPtr>();
            List<string> titles = new List<string>();
            List<uint> pids = new List<uint>();
            int seen = 0;
            _skipTool = 0;
            _skipOwned = 0;
            _skipCloaked = 0;
            _skipTitle = 0;

            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsCandidate(h, selfPid)) return true;

                string title = TitleOf(h);
                if (title.Length == 0)
                {
                    _skipTitle++;
                    return true;
                }

                seen++;
                if (handles.Count >= max) return true;

                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == 0) return true;

                handles.Add(h);
                titles.Add(title);
                pids.Add(pid);
                return true;
            }, IntPtr.Zero);

            total = seen;
            LastDiagnostics = "kept=" + handles.Count + " total=" + seen
                + " skipTool=" + _skipTool + " skipOwned=" + _skipOwned
                + " skipCloaked=" + _skipCloaked + " skipNoTitle=" + _skipTitle;

            List<WindowInfo> list = new List<WindowInfo>(handles.Count);
            for (int i = 0; i < handles.Count; i++)
            {
                WindowInfo info = new WindowInfo();
                info.Hwnd = handles[i];
                info.Title = titles[i];
                info.Pid = pids[i];
                info.Minimized = IsIconic(handles[i]);
                info.TopMost = IsTopMost(handles[i]);
                string[] proc = ProcessInfo(pids[i]);
                info.ProcessName = proc[0];
                info.ExePath = proc[1];
                list.Add(info);
            }
            return list;
        }

        public static bool Exists(IntPtr hwnd)
        {
            return hwnd != IntPtr.Zero && IsWindow(hwnd);
        }

        public static bool CloseWindow(IntPtr hwnd)
        {
            if (!Exists(hwnd)) return false;
            return PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        public static bool Minimize(IntPtr hwnd)
        {
            if (!Exists(hwnd)) return false;
            return Native.ShowWindow(hwnd, SW_MINIMIZE);
        }

        public static bool ToggleMaximize(IntPtr hwnd)
        {
            if (!Exists(hwnd)) return false;
            return Native.ShowWindow(hwnd, IsZoomed(hwnd) ? SW_RESTORE : SW_MAXIMIZE);
        }

        public static bool IsTopMost(IntPtr hwnd)
        {
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            return (exStyle & WS_EX_TOPMOST) != 0;
        }

        public static bool ToggleTopMost(IntPtr hwnd)
        {
            if (!Exists(hwnd)) return false;
            IntPtr after = IsTopMost(hwnd) ? new IntPtr(-2) : new IntPtr(-1);
            return Native.SetWindowPos(hwnd, after, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        /// <summary>
        /// 把窗口移到下一块显示器并居中；只有一块屏时返回 false。
        /// </summary>
        public static bool MoveToNextMonitor(IntPtr hwnd)
        {
            if (!Exists(hwnd)) return false;
            try
            {
                if (IsZoomed(hwnd)) Native.ShowWindow(hwnd, SW_RESTORE);

                IntPtr current = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                List<IntPtr> monitors = new List<IntPtr>();
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(IntPtr hMon, IntPtr hdc, IntPtr rect, IntPtr data)
                {
                    monitors.Add(hMon);
                    return true;
                }, IntPtr.Zero);
                if (monitors.Count < 2) return false;

                int index = monitors.IndexOf(current);
                if (index < 0) index = 0;
                IntPtr target = monitors[(index + 1) % monitors.Count];

                Native.MONITORINFO mi = new Native.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
                if (!Native.GetMonitorInfo(target, ref mi)) return false;

                Native.RECT wr;
                if (!Native.GetWindowRect(hwnd, out wr)) return false;

                int w = wr.Right - wr.Left;
                int h = wr.Bottom - wr.Top;
                int workW = mi.rcWork.Right - mi.rcWork.Left;
                int workH = mi.rcWork.Bottom - mi.rcWork.Top;
                int x = mi.rcWork.Left + Math.Max(0, (workW - w) / 2);
                int y = mi.rcWork.Top + Math.Max(0, (workH - h) / 2);

                return Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
                    Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            }
            catch
            {
                return false;
            }
        }

        public static bool Activate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                if (IsIconic(hwnd))
                {
                    if (!Native.ShowWindow(hwnd, SW_RESTORE))
                        SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                }

                Native.SetForegroundWindow(hwnd);
                if (Native.GetForegroundWindow() != hwnd)
                {
                    Native.SwitchToThisWindow(hwnd, true);
                    Native.SetForegroundWindow(hwnd);
                }

                IntPtr fg = Native.GetForegroundWindow();
                bool ok = fg == hwnd;
                Log.Write("activate target=" + hwnd.ToInt64().ToString("x")
                    + " foreground=" + fg.ToInt64().ToString("x")
                    + " ok=" + ok);
                return ok;
            }
            catch (Exception ex)
            {
                Log.Exception("Activate", ex);
                return false;
            }
        }

        private static bool IsCandidate(IntPtr h, uint selfPid)
        {
            if (h == IntPtr.Zero) return false;
            if (!IsWindowVisible(h)) return false;

            int exStyle = GetWindowLong(h, GWL_EXSTYLE);
            bool tool = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool app = (exStyle & WS_EX_APPWINDOW) != 0;

            // 与任务栏 / Alt+Tab 一致：工具窗口不进列表，除非显式标了 APPWINDOW
            if (tool && !app)
            {
                _skipTool++;
                return false;
            }

            // 有 owner 但没标 APPWINDOW 的才排除（百度网盘这类绿色版主窗口带 owner，
            // 但带 WS_EX_APPWINDOW，Alt+Tab 会显示，所以不能一律按 owner 过滤）
            if (GetWindow(h, GW_OWNER) != IntPtr.Zero && !app)
            {
                _skipOwned++;
                return false;
            }

            int cloaked;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                _skipCloaked++;
                return false;
            }

            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid == 0 || pid == selfPid) return false;

            return true;
        }

        private static string TitleOf(IntPtr h)
        {
            int len = GetWindowTextLength(h);
            if (len <= 0) return "";
            StringBuilder sb = new StringBuilder(len + 2);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString().Trim();
        }

        private static string[] ProcessInfo(uint pid)
        {
            string[] cached;
            if (ProcessCache.TryGetValue(pid, out cached)) return cached;

            string name = "";
            string path = "";
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    name = p.ProcessName;
                    try { path = p.MainModule.FileName; }
                    catch { }
                }
            }
            catch
            {
            }

            string[] info = new string[] { name, path };
            if (ProcessCache.Count > 256) ProcessCache.Clear();
            ProcessCache[pid] = info;
            return info;
        }
    }
}
