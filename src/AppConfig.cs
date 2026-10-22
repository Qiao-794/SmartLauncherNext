using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SmartLauncher
{
    public class Item
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string Key { get; set; }
        public string IconPath { get; set; }
        public string Args { get; set; }

        public Item()
        {
            Name = "";
            Type = "quicker";
            Path = "";
            Key = "";
            IconPath = "";
            Args = "";
        }
    }

    public class Category
    {
        public string Name { get; set; }
        public List<Item> Items { get; set; }

        public Category()
        {
            Name = "";
            Items = new List<Item>();
        }
    }

    public class AppConfig
    {
        public int SchemaVersion { get; set; }
        public bool HideWhenInactive { get; set; }
        public List<Category> Categories { get; set; }
        public int IdleExitMinutes { get; set; }
        public bool StartWithWindows { get; set; }

        public AppConfig()
        {
            SchemaVersion = 2;
            HideWhenInactive = true;
            Categories = new List<Category>();
            IdleExitMinutes = 5;
            StartWithWindows = false;
        }
    }

    public static class ConfigStore
    {
        public static string BaseDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(BaseDir, "launcher.json"); }
        }

        public static string IconDir
        {
            get { return Path.Combine(BaseDir, "Cache", "Icons"); }
        }

        public static readonly string[] LegacyDirs = new string[]
        {
            @"D:\Program Files\SmartLauncher_Lite",
            @"D:\Toolo\SmartLauncher_Next_Lite",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartLauncher_Lite")
        };

        public static AppConfig LoadFrom(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                AppConfig c = Deserialize(File.ReadAllText(path, Encoding.UTF8));
                if (c == null) return null;
                Normalize(c);
                return c;
            }
            catch
            {
                return null;
            }
        }
        public static AppConfig Load(out bool imported)
        {
            imported = false;
            try
            {
                if (File.Exists(ConfigPath))
                {
                    AppConfig c = Deserialize(File.ReadAllText(ConfigPath, Encoding.UTF8));
                    if (c != null)
                    {
                        Normalize(c);
                        return c;
                    }
                }
            }
            catch
            {
            }

            string source;
            AppConfig legacy = ImportLegacyInternal(out source, true);
            if (legacy != null)
            {
                imported = true;
                return legacy;
            }

            return Default();
        }

        public static AppConfig ImportLegacy(out string source)
        {
            return ImportLegacyInternal(out source, false);
        }

        private static AppConfig ImportLegacyInternal(out string source, bool save)
        {
            source = null;
            foreach (string dir in LegacyDirs)
            {
                try
                {
                    string p = Path.Combine(dir, "launcher.json");
                    if (!File.Exists(p)) continue;
                    AppConfig c = Deserialize(File.ReadAllText(p, Encoding.UTF8));
                    if (c == null) continue;
                    Normalize(c);
                    CopyIcons(dir);
                    source = dir;
                    if (save) { try { Save(c); } catch { } }
                    return c;
                }
                catch
                {
                }
            }
            return null;
        }

        private static void CopyIcons(string legacyDir)
        {
            try
            {
                string src = Path.Combine(legacyDir, "Cache", "Icons");
                if (!Directory.Exists(src)) return;
                Directory.CreateDirectory(IconDir);
                foreach (string f in Directory.GetFiles(src))
                {
                    try
                    {
                        string dst = Path.Combine(IconDir, Path.GetFileName(f));
                        if (!File.Exists(dst)) File.Copy(f, dst);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        public static void SaveTo(AppConfig cfg, string path)
        {
            if (cfg == null) throw new ArgumentNullException("cfg");
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            cfg.SchemaVersion = 2;
            string json = Serializer().Serialize(cfg);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        public static void Save(AppConfig cfg)
        {
            if (cfg != null) cfg.SchemaVersion = 2;
            JavaScriptSerializer ser = Serializer();
            string json = ser.Serialize(cfg);
            string dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            try
            {
                if (File.Exists(ConfigPath))
                    File.Copy(ConfigPath, ConfigPath + ".bak", true);
                File.Copy(tmp, ConfigPath, true);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        public static AppConfig Clone(AppConfig cfg)
        {
            JavaScriptSerializer ser = Serializer();
            AppConfig c = ser.Deserialize<AppConfig>(ser.Serialize(cfg));
            Normalize(c);
            return c;
        }

        public static AppConfig Default()
        {
            AppConfig c = new AppConfig();
            Category a = new Category();
            a.Name = "软件";
            Category b = new Category();
            b.Name = "网页";
            c.Categories.Add(a);
            c.Categories.Add(b);
            return c;
        }

        private static AppConfig Deserialize(string json)
        {
            JavaScriptSerializer ser = Serializer();
            return ser.Deserialize<AppConfig>(json);
        }

        private static JavaScriptSerializer Serializer()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            ser.RecursionLimit = 100;
            return ser;
        }

        private static void Normalize(AppConfig c)
        {
            if (c == null) return;
            if (c.SchemaVersion <= 0) c.SchemaVersion = 2;
            if (c.Categories == null) c.Categories = new List<Category>();
            if (c.IdleExitMinutes < 0) c.IdleExitMinutes = 5;
            foreach (Category cat in c.Categories)
            {
                if (cat == null) continue;
                if (cat.Name == null) cat.Name = "";
                if (cat.Items == null) cat.Items = new List<Item>();
                foreach (Item it in cat.Items)
                {
                    if (it == null) continue;
                    if (it.Name == null) it.Name = "";
                    if (it.Type == null || it.Type.Length == 0) it.Type = "quicker";
                    if (it.Path == null) it.Path = "";
                    if (it.Key == null) it.Key = "";
                    if (it.IconPath == null) it.IconPath = "";
                    if (it.Args == null) it.Args = "";
                }
            }
        }
    }
}
