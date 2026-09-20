using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace DIYAmbient.Core
{
    public enum LightingMode { White, Solid, Screen }

    [DataContract]
    public sealed class DisplayMap
    {
        [DataMember(IsRequired = true)] public string DeviceName;
        [DataMember(IsRequired = true)] public int Position; // -1 left, 0 center, +1 right
        [DataMember(IsRequired = true)] public int FirstLed;
        [DataMember(IsRequired = true)] public int LastLed;
        public DisplayMap Clone() { return (DisplayMap)MemberwiseClone(); }
    }

    [DataContract]
    public sealed class LedZone
    {
        [DataMember(IsRequired = true)] public int Led; // 1-based physical address
        [DataMember(IsRequired = true)] public string DeviceName;
        [DataMember(IsRequired = true)] public double X;
        [DataMember(IsRequired = true)] public double Y;
        [DataMember(IsRequired = true)] public double Width;
        [DataMember(IsRequired = true)] public double Height;
        public LedZone Clone() { return (LedZone)MemberwiseClone(); }
    }

    [DataContract]
    public sealed class Settings
    {
        public const int LedCount = 60; // Existing firmware ALWAYS consumes 60 RGB triples.
        [DataMember(IsRequired = true)] public int SchemaVersion;
        [DataMember(IsRequired = true)] public string SerialPort;
        [DataMember(IsRequired = true)] public bool PreviewOnly;
        [DataMember(IsRequired = true)] public bool ElectricalConfirmed;
        [DataMember(IsRequired = true)] public LightingMode Mode;
        [DataMember(IsRequired = true)] public double Brightness;
        [DataMember(IsRequired = true)] public int TelemetryLedCount;
        [DataMember(IsRequired = true)] public byte SolidR;
        [DataMember(IsRequired = true)] public byte SolidG;
        [DataMember(IsRequired = true)] public byte SolidB;
        [DataMember(IsRequired = true)] public double Warmth;
        [DataMember(IsRequired = true)] public double Tint;
        [DataMember(IsRequired = true)] public double CurrentBudgetAmps;
        [DataMember(IsRequired = true)] public List<DisplayMap> Displays;
        [DataMember(IsRequired = true)] public List<LedZone> Zones;

        public Settings()
        {
            SchemaVersion = 1; SerialPort = ""; PreviewOnly = true;
            ElectricalConfirmed = false; Mode = LightingMode.White; Brightness = 0.25;
            TelemetryLedCount = 0; SolidR = 255; SolidG = 180; SolidB = 90;
            Warmth = 0; Tint = 0;
            // Provisional budget, NOT a certified safe rating for unknown wiring.
            CurrentBudgetAmps = 0.5;
            Displays = new List<DisplayMap> {
                new DisplayMap { DeviceName = @"\\.\DISPLAY2", Position = -1, FirstLed = 1, LastLed = 20 },
                new DisplayMap { DeviceName = @"\\.\DISPLAY1", Position = 0, FirstLed = 21, LastLed = 40 },
                new DisplayMap { DeviceName = @"\\.\DISPLAY3", Position = 1, FirstLed = 41, LastLed = 60 }
            };
            Zones = new List<LedZone>();
            foreach (DisplayMap display in Displays) Zones.AddRange(DefaultZones(display));
        }

        public static IEnumerable<LedZone> DefaultZones(DisplayMap display)
        {
            int count = display.LastLed - display.FirstLed + 1;
            for (int i = 0; i < count; i++)
            {
                // A starting perimeter only: the real physical route is set in the editor.
                double t = 4.0 * i / count, x, y;
                if (t < 1) { x = t * .88; y = 0; }
                else if (t < 2) { x = .88; y = (t - 1) * .88; }
                else if (t < 3) { x = (3 - t) * .88; y = .88; }
                else { x = 0; y = (4 - t) * .88; }
                yield return new LedZone { Led = display.FirstLed + i, DeviceName = display.DeviceName,
                    X = x, Y = y, Width = .12, Height = .12 };
            }
        }

        public Settings Clone()
        {
            Settings s = (Settings)MemberwiseClone();
            s.Displays = Displays.Select(d => d.Clone()).ToList();
            s.Zones = Zones.Select(z => z.Clone()).ToList();
            return s;
        }

        public void Validate()
        {
            Require(SchemaVersion == 1, "Version de configuration inconnue.");
            Require(Enum.IsDefined(typeof(LightingMode), Mode), "Mode inconnu.");
            Range(Brightness, 0, 1, "Luminosité");
            Range(Warmth, -1, 1, "Température visuelle");
            Range(Tint, -1, 1, "Teinte du blanc");
            Range(CurrentBudgetAmps, .060, 5, "Budget estimé du ruban (A)");
            Require(TelemetryLedCount >= 0 && TelemetryLedCount <= 60 && TelemetryLedCount % 2 == 0,
                "Le nombre de LED de télémétrie doit être pair, entre 0 et 60.");
            Require(Displays != null && Displays.Count == 3 && Displays.All(d => d != null), "Trois écrans requis.");
            Require(Displays.Select(d => d.Position).OrderBy(p => p).SequenceEqual(new[] { -1, 0, 1 }), "Rôles écran invalides.");
            Require(Displays.All(d => !string.IsNullOrWhiteSpace(d.DeviceName)), "Identifiant écran manquant.");
            Require(Displays.Select(d => d.DeviceName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3,
                "Chaque rôle doit désigner un écran différent.");
            var covered = new List<int>();
            foreach (DisplayMap d in Displays)
            {
                Require(d.FirstLed >= 1 && d.LastLed <= 60 && d.FirstLed <= d.LastLed, "Plage LED invalide.");
                covered.AddRange(Enumerable.Range(d.FirstLed, d.LastLed - d.FirstLed + 1));
            }
            Require(covered.OrderBy(i => i).SequenceEqual(Enumerable.Range(1, 60)),
                "Les plages doivent couvrir exactement les 60 LED, sans trou ni doublon.");
            Require(Zones != null && Zones.Count == 60 && Zones.All(z => z != null), "60 zones requises.");
            Require(Zones.Select(z => z.Led).OrderBy(i => i).SequenceEqual(Enumerable.Range(1, 60)), "Adresses LED invalides.");
            foreach (LedZone z in Zones)
            {
                DisplayMap d = Displays.SingleOrDefault(m => string.Equals(m.DeviceName, z.DeviceName, StringComparison.OrdinalIgnoreCase));
                Require(d != null && z.Led >= d.FirstLed && z.Led <= d.LastLed, "Zone hors de sa plage écran.");
                Range(z.X, 0, 1, "X"); Range(z.Y, 0, 1, "Y");
                Range(z.Width, .005, 1, "Largeur"); Range(z.Height, .005, 1, "Hauteur");
                Require(z.X + z.Width <= 1.0000001 && z.Y + z.Height <= 1.0000001, "Zone hors écran.");
            }
            Require(SerialPort != null && (SerialPort.Length == 0 ||
                Regex.IsMatch(SerialPort, @"\ACOM[1-9][0-9]{0,4}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
                "Port invalide : choisir un nom COM Windows (par exemple COM3).");
        }

        private static void Range(double x, double min, double max, string label)
        {
            Require(!double.IsNaN(x) && !double.IsInfinity(x) && x >= min && x <= max, label + " : valeur invalide.");
        }
        private static void Require(bool ok, string message) { if (!ok) throw new ArgumentException(message); }
    }
}
