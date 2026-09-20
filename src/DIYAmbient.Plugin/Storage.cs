using System;
using System.IO;
using System.Runtime.Serialization.Json;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal static class Storage
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DIY-Ambient");
        public static readonly string SettingsPath = Path.Combine(Folder, "settings.json");
        private static readonly object Gate = new object();

        public static Settings Load(out string warning)
        {
            warning = "";
            if (!File.Exists(SettingsPath)) return new Settings();
            try
            {
                using (var stream = File.OpenRead(SettingsPath))
                {
                    if (stream.Length > 1024 * 1024) throw new InvalidDataException("Configuration trop volumineuse.");
                    var s = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                    if (s == null) throw new InvalidDataException("Configuration vide.");
                    s.Validate();
                    return s;
                }
            }
            catch (Exception ex)
            {
                warning = "Configuration illisible : paramètres prudents restaurés, matériel désactivé.";
                try { File.Copy(SettingsPath, SettingsPath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), false); }
                catch (Exception backupError) { Log("Configuration backup: " + backupError.Message); }
                Log("Configuration load: " + ex.Message);
                return new Settings();
            }
        }

        public static void Save(Settings s)
        {
            s.Validate();
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                string temp = SettingsPath + ".tmp";
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, s); stream.Flush(true); }
                if (File.Exists(SettingsPath)) File.Replace(temp, SettingsPath, SettingsPath + ".bak", true);
                else File.Move(temp, SettingsPath);
            }
        }

        public static void Log(string message)
        {
            // Never log frames or screenshots; cap storage and never propagate logging failures.
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    string path = Path.Combine(Folder, "diagnostic.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
                }
            }
            catch { /* Logging cannot be allowed to take down the host. */ }
        }
    }
}
