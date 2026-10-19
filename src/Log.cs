using System;
using System.IO;
using System.Text;

namespace SmartLauncher
{
    public static class Log
    {
        private static readonly object Sync = new object();

        public static string FilePath
        {
            get { return Path.Combine(ConfigStore.BaseDir, "debug.log"); }
        }

        public static void Trim()
        {
            try
            {
                FileInfo fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 256 * 1024) File.Delete(FilePath);
            }
            catch
            {
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }

        public static void Exception(string where, Exception ex)
        {
            try
            {
                Write(where + ": " + ex.ToString());
            }
            catch
            {
            }
        }
    }
}
