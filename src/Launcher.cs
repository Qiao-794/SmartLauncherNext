using System;
using System.Diagnostics;
using System.IO;

namespace SmartLauncher
{
    public static class Launcher
    {
        public static bool TryRun(Item item, out string error)
        {
            error = null;
            if (item == null)
            {
                error = "条目为空";
                return false;
            }

            try
            {
                string type = (item.Type ?? "").Trim().ToLowerInvariant();
                if (type.Length == 0) type = "quicker";

                if (type == "quicker")
                {
                    if (string.IsNullOrWhiteSpace(item.Path))
                    {
                        error = "没有填写 Quicker 动作 ID";
                        return false;
                    }
                    string uri = "quicker:runaction:" + Uri.EscapeDataString(item.Path.Trim());
                    ProcessStartInfo psi = new ProcessStartInfo(uri);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    return true;
                }

                if (type == "exe")
                {
                    string path = Environment.ExpandEnvironmentVariables((item.Path ?? "").Trim());
                    if (path.Length == 0)
                    {
                        error = "没有填写程序路径";
                        return false;
                    }
                    if (!File.Exists(path))
                    {
                        error = "找不到程序：" + path;
                        return false;
                    }
                    ProcessStartInfo psi = new ProcessStartInfo(path);
                    psi.UseShellExecute = true;
                    psi.WorkingDirectory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(item.Args)) psi.Arguments = item.Args;
                    Process.Start(psi);
                    return true;
                }

                if (type == "url")
                {
                    string url = (item.Path ?? "").Trim();
                    if (url.Length == 0)
                    {
                        error = "没有填写网址";
                        return false;
                    }
                    if (url.IndexOf("://", StringComparison.Ordinal) < 0 && !url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                        url = "https://" + url;
                    ProcessStartInfo psi = new ProcessStartInfo(url);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    return true;
                }

                error = "未知的启动类型：" + item.Type;
                return false;
            }
            catch (Exception ex)
            {
                error = "启动失败：" + ex.Message;
                return false;
            }
        }
    }
}
