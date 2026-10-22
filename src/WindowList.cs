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
        private const uint GW_OWNER = 4;
        private const int DWMWA_CLOAKED = 14;
        private const int SW_RESTORE = 9;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;

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
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);

        private static readonly Dictionary<uint, string[]> ProcessCache = new Dictionary<uint, string[]>();

        public static List<WindowInfo> Enumerate(uint selfPid, int max, out int total)
        {
            List<IntPtr> handles = new List<IntPtr>();
            List<string> titles = new List<string>();
            List<uint> pids = new List<uint>();
            int seen = 0;

            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsCandidate(h, selfPid)) return true;

                string title = TitleOf(h);
                if (title.Length == 0) return true;

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

            List<WindowInfo> list = new List<WindowInfo>(handles.Count);
            for (int i = 0; i < handles.Count; i++)
            {
                WindowInfo info = new WindowInfo();
                info.Hwnd = handles[i];
                info.Title = titles[i];
                info.Pid = pids[i];
                info.Minimized = IsIconic(handles[i]);
                string[] proc = ProcessInfo(pids[i]);
                info.ProcessName = proc[0];
                info.ExePath = proc[1];
                list.Add(info);
            }
            return list;
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
            if (tool && !app) return false;

            if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return false;

            int cloaked;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, sizeof(int)) == 0 && cloaked != 0)
                return false;

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
