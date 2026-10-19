using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartLauncher
{
    internal static class Hooks
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr extra;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        private static IntPtr _keyboardHook;
        private static HookProc _keyboardProc;
        private static Func<int, bool> _onKey;

        public static bool IsInstalled
        {
            get { return _keyboardHook != IntPtr.Zero; }
        }

        public static bool ModifierDown
        {
            get { return Down(0x11) || Down(0x12) || Down(0x5B) || Down(0x5C); }
        }

        public static bool Install(Func<int, bool> onKey)
        {
            Uninstall();
            _onKey = onKey;
            _keyboardProc = KeyboardProc;
            IntPtr hMod = GetModuleHandle(null);
            _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            return IsInstalled;
        }

        public static void Uninstall()
        {
            if (_keyboardHook != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(_keyboardHook); } catch { }
                _keyboardHook = IntPtr.Zero;
            }
            _onKey = null;
        }

        public static bool IsOurWindow(int x, int y)
        {
            POINT p;
            p.X = x;
            p.Y = y;
            IntPtr under = WindowFromPoint(p);
            if (under == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(under, out pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }

        public static string TokenForVk(int vk)
        {
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return (vk - 0x60).ToString();
            if (vk >= 0x70 && vk <= 0x7B) return "F" + (vk - 0x70 + 1);
            return null;
        }

        private static bool Down(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && _onKey != null)
                {
                    int msg = wParam.ToInt32();
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        KBDLLHOOKSTRUCT k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                        if (_onKey((int)k.vkCode)) return (IntPtr)1;
                    }
                }
            }
            catch
            {
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
    }
}
