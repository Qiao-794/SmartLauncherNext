using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SmartLauncher
{
    internal static class Program
    {
        public static AppConfig Config;
        public static PanelWindow Panel;

        private static Mutex _mutex;
        private static EventWaitHandle _showEvent;
        private static EventWaitHandle _toggleEvent;
        private static EventWaitHandle _settingsEvent;
        private static Thread _ipcThread;
        private static DateTime _lastActivity = DateTime.Now;
        private static string InstancePrefix = "Local\\SmartLauncherNext.SL2.";

        public static string ExePath
        {
            get
            {
                try { return Process.GetCurrentProcess().MainModule.FileName; }
                catch { return System.Reflection.Assembly.GetEntryAssembly().Location; }
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Exception("AppDomain", e.ExceptionObject as Exception);
            };

            Log.Trim();
            Log.Write("Main start: " + string.Join(" ", args));
            try
            {
                Run(args);
                Log.Write("Main end");
            }
            catch (Exception ex)
            {
                Log.Exception("Main", ex);
            }
        }

        private static void Run(string[] args)
        {
            bool show = false;
            bool settings = false;
            bool pinned = false;

            foreach (string a in args)
            {
                if (string.Equals(a, "--show", StringComparison.OrdinalIgnoreCase)) show = true;
                else if (string.Equals(a, "--toggle", StringComparison.OrdinalIgnoreCase)) show = true;
                else if (string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase)) settings = true;
                else if (string.Equals(a, "--pin", StringComparison.OrdinalIgnoreCase)) pinned = true;
                else if (string.Equals(a, "--dark", StringComparison.OrdinalIgnoreCase)) Theme.OverrideTheme = 1;
                else if (string.Equals(a, "--light", StringComparison.OrdinalIgnoreCase)) Theme.OverrideTheme = 0;
            }

            foreach (string a in args)
            {
                if (a.StartsWith("--instance=", StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = a.Substring("--instance=".Length).Trim();
                    if (suffix.Length > 0 && suffix.Length <= 32)
                        InstancePrefix = "Local\\SmartLauncherNext." + suffix + ".";
                }
            }

            Theme.Load();
            Log.Write("theme loaded dark=" + Theme.IsDark);

            bool imported;
            Config = ConfigStore.Load(out imported);
            Log.Write("config loaded imported=" + imported + " cats=" + Config.Categories.Count);

            bool created;
            _mutex = new Mutex(true, InstancePrefix + "Instance", out created);
            if (!created)
            {
                Log.Write("second instance, signaling");
                if (Signal(show, settings)) return;

                Log.Write("signal failed, waiting for old instance to exit");
                try
                {
                    if (!_mutex.WaitOne(3000))
                    {
                        Log.Write("mutex wait timeout");
                        return;
                    }
                }
                catch
                {
                    return;
                }
                Log.Write("took over instance mutex");
            }

            try { Autostart.Apply(Config.StartWithWindows, ExePath); }
            catch { }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Exception("Dispatcher", e.Exception);
            };
            Log.Write("application created");

            Panel = new PanelWindow(Config, pinned);
            Log.Write("panel constructed");
            Panel.Activity += delegate { _lastActivity = DateTime.Now; };

            StartIpc(app.Dispatcher);
            Log.Write("ipc started");

            DispatcherTimer idle = new DispatcherTimer();
            idle.Interval = TimeSpan.FromSeconds(10);
            idle.Tick += delegate
            {
                if (Config == null || Config.IdleExitMinutes <= 0) return;
                if (Panel.IsVisible || Panel.SettingsOpen)
                {
                    _lastActivity = DateTime.Now;
                    return;
                }
                if ((DateTime.Now - _lastActivity).TotalMinutes >= Config.IdleExitMinutes)
                    Application.Current.Shutdown();
            };
            idle.Start();

            if (show || settings)
            {
                app.Dispatcher.BeginInvoke(new Action(delegate
                {
                    Log.Write("initial action settings=" + settings);
                    if (settings) Panel.OpenSettings();
                    else Panel.ShowPanel();
                    Log.Write("initial action done");
                }));
            }

            app.Run();
            Log.Write("app run ended");
            StopIpc();
        }

        public static void SaveConfig(AppConfig cfg)
        {
            ConfigStore.Save(cfg);
            Config = cfg;
            try { Autostart.Apply(cfg.StartWithWindows, ExePath); }
            catch { }
            if (Panel != null) Panel.Reload(cfg);
        }

        private static void StartIpc(Dispatcher dispatcher)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstancePrefix + "Show");
            _toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstancePrefix + "Toggle");
            _settingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstancePrefix + "Settings");

            _ipcThread = new Thread(delegate()
            {
                WaitHandle[] handles = new WaitHandle[] { _showEvent, _toggleEvent, _settingsEvent };
                while (true)
                {
                    int index;
                    try { index = WaitHandle.WaitAny(handles); }
                    catch { return; }
                    if (index == WaitHandle.WaitTimeout) continue;

                    int captured = index;
                    dispatcher.BeginInvoke(new Action(delegate
                    {
                        Log.Write("ipc action " + captured);
                        if (captured == 0) Panel.ShowPanel();
                        else if (captured == 1)
                        {
                            if (Panel.IsVisible) Panel.HideNow();
                            else Panel.ShowPanel();
                        }
                        else Panel.OpenSettings();
                    }));
                }
            });
            _ipcThread.IsBackground = true;
            _ipcThread.Start();
        }

        private static void StopIpc()
        {
            try
            {
                if (_showEvent != null) _showEvent.Close();
                if (_toggleEvent != null) _toggleEvent.Close();
                if (_settingsEvent != null) _settingsEvent.Close();
                if (_mutex != null) _mutex.ReleaseMutex();
            }
            catch
            {
            }
        }

        private static bool Signal(bool show, bool settings)
        {
            string suffix = settings ? "Settings" : (show ? "Show" : "Toggle");
            EventWaitHandle ev = null;
            for (int i = 0; i < 40; i++)
            {
                try
                {
                    ev = EventWaitHandle.OpenExisting(InstancePrefix + suffix);
                    break;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
            if (ev == null) return false;
            try
            {
                ev.Set();
            }
            catch
            {
                return false;
            }
            finally
            {
                try { ev.Close(); } catch { }
            }
            return true;
        }
    }
}

