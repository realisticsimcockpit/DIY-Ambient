using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    [DataContract]
    internal sealed class ProfileEntry
    {
        [DataMember(IsRequired = true)] public string Name;
        [DataMember(IsRequired = true)] public Settings Settings;
    }

    [DataContract]
    internal sealed class ProfileCollection
    {
        [DataMember(IsRequired = true)] public List<ProfileEntry> Profiles = new List<ProfileEntry>();
    }

    internal static class Profiles
    {
        private static readonly object Gate = new object();
        private static readonly string PathName = Path.Combine(Storage.Folder, "profiles.json");

        internal static string[] Names()
        {
            lock (Gate) return Load().Profiles.Select(p => p.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        internal static void Save(string name, Settings settings)
        {
            name = CleanName(name);
            settings.Validate();
            lock (Gate)
            {
                ProfileCollection data = Load();
                ProfileEntry existing = data.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing == null) data.Profiles.Add(new ProfileEntry { Name = name, Settings = settings.Clone() });
                else { existing.Name = name; existing.Settings = settings.Clone(); }
                Write(data);
            }
        }

        internal static bool TryLoad(string name, out Settings settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (Gate)
            {
                ProfileEntry entry = Load().Profiles.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.Settings == null) return false;
                entry.Settings.Validate(); settings = entry.Settings.Clone(); return true;
            }
        }

        private static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 80) throw new ArgumentException("Nom de profil invalide.");
            return name;
        }

        private static ProfileCollection Load()
        {
            if (!File.Exists(PathName)) return new ProfileCollection();
            try
            {
                using (var stream = File.OpenRead(PathName))
                {
                    var data = (ProfileCollection)new DataContractJsonSerializer(typeof(ProfileCollection)).ReadObject(stream);
                    return data ?? new ProfileCollection();
                }
            }
            catch (Exception ex) { Storage.Log("Profiles load: " + ex.Message); return new ProfileCollection(); }
        }

        private static void Write(ProfileCollection data)
        {
            Directory.CreateDirectory(Storage.Folder);
            string temp = PathName + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { new DataContractJsonSerializer(typeof(ProfileCollection)).WriteObject(stream, data); stream.Flush(true); }
            if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak", true);
            else File.Move(temp, PathName);
        }
    }
}
