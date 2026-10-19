using System;
using Microsoft.Win32;

namespace SmartLauncher
{
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SmartLauncherNext";

        public static void Apply(bool enabled, string exePath)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (k == null) return;
                if (enabled && !string.IsNullOrEmpty(exePath))
                    k.SetValue(ValueName, "\"" + exePath + "\" --background");
                else
                    k.DeleteValue(ValueName, false);
            }
        }
    }
}
