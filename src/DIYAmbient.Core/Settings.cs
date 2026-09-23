using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace DIYAmbient.Core
{
    public enum LightingMode { White, Solid, Screen, Animation, Rpm }
    public enum SpotterColor { Red, Orange, Purple, Pink, White }

    [DataContract]
    public sealed class AnimationPreference
    {
        [DataMember] public AnimationEffect Effect;
        [DataMember] public int Speed;
        [DataMember] public int Intensity;
        [DataMember] public bool RandomPalette;
        public AnimationPreference Clone() { return (AnimationPreference)MemberwiseClone(); }
    }
    // Numeric values 4, 5 and 9 preserve profiles created by v0.2.0.
    public enum AnimationEffect { Colorloop = 4, Rainbow = 5, FireFlicker = 9, Loading = 47 }

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
        [DataMember(IsRequired = false)] public bool KeepOnAfterExit;
        [DataMember(IsRequired = false)] public int LedStripCount;
        [DataMember(IsRequired = false)] public bool StartEnabled;
        [DataMember(IsRequired = false)] public bool StartEnabledPreferenceInitialized;
        [DataMember(IsRequired = true)] public LightingMode Mode;
        [DataMember(IsRequired = false)] public LightingMode IdleMode;
        [DataMember(IsRequired = false)] public LightingMode InGameMode;
        [DataMember(IsRequired = false)] public bool ModeProfilesInitialized;
        [DataMember(IsRequired = true)] public double Brightness;
        [DataMember(IsRequired = true)] public int TelemetryLedCount;
        [DataMember(IsRequired = false)] public bool TelemetryEffectsInitialized;
        [DataMember(IsRequired = false)] public bool TelemetrySpotterEnabled;
        [DataMember] public SpotterColor SpotterColor;
        [DataMember] public List<AnimationPreference> AnimationPreferences;
        [DataMember(IsRequired = false)] public bool TelemetryYellowEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryBlueEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryGreenEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryWhiteEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryInPitEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryBlackEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryOrangeEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryCheckeredEnabled;
        [DataMember(IsRequired = false)] public bool DrivingEffectsInitialized;
        [DataMember(IsRequired = false)] public bool TelemetryAbsEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryTcEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryWheelLockEnabled;
        [DataMember(IsRequired = false)] public bool TelemetryRpmEnabled;
        [DataMember(IsRequired = true)] public byte SolidR;
        [DataMember(IsRequired = true)] public byte SolidG;
        [DataMember(IsRequired = true)] public byte SolidB;
        [DataMember(IsRequired = false)] public AnimationEffect AnimationEffect;
        [DataMember(IsRequired = false)] public int AnimationSpeed;
        [DataMember(IsRequired = false)] public int AnimationIntensity;
        [DataMember(IsRequired = false)] public bool AnimationRandomPalette;
        [DataMember(IsRequired = false)] public bool AnimationPaletteInitialized;
        [DataMember(IsRequired = true)] public double Warmth;
        [DataMember(IsRequired = true)] public double Tint;
        [DataMember(IsRequired = true)] public double CurrentBudgetAmps;
        [DataMember(IsRequired = true)] public List<DisplayMap> Displays;
        [DataMember(IsRequired = true)] public List<LedZone> Zones;

        public Settings()
        {
            SchemaVersion = 1; SerialPort = ""; PreviewOnly = false;
            ElectricalConfirmed = true; KeepOnAfterExit = false; LedStripCount = 3;
            StartEnabled = true; StartEnabledPreferenceInitialized = true;
            Mode = LightingMode.White; Brightness = 0.25;
            IdleMode = LightingMode.White; InGameMode = LightingMode.Screen; ModeProfilesInitialized = true;
            TelemetryLedCount = 0; InitializeTelemetryEffects();
            SolidR = 255; SolidG = 180; SolidB = 90;
            AnimationEffect = AnimationEffect.Colorloop; AnimationSpeed = 128; AnimationIntensity = 128;
            AnimationRandomPalette = true; AnimationPaletteInitialized = true;
            Warmth = 0; Tint = 0;
            // Provisional budget, NOT a certified safe rating for unknown wiring.
            CurrentBudgetAmps = 15.0;
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
            return ZoneLayout.CenteredStrips(display);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            // Configurations created before the strip selector represent the 3-strip installation.
            if (LedStripCount == 0) LedStripCount = 3;
            if (AnimationSpeed == 0) AnimationSpeed = 128;
            if (AnimationIntensity == 0) AnimationIntensity = 128;
            if (!Enum.IsDefined(typeof(AnimationEffect), AnimationEffect)) AnimationEffect = AnimationEffect.Colorloop;
            if (!AnimationPaletteInitialized) { AnimationRandomPalette = true; AnimationPaletteInitialized = true; }
            if (!TelemetryEffectsInitialized) InitializeTelemetryEffects();
            if (!DrivingEffectsInitialized) InitializeDrivingEffects();
            if (TelemetryRpmEnabled) { Mode = LightingMode.Rpm; TelemetryRpmEnabled = false; }
            if (!ModeProfilesInitialized)
            {
                IdleMode = Mode == LightingMode.Screen || Mode == LightingMode.Rpm ? LightingMode.White : Mode;
                InGameMode = Mode; ModeProfilesInitialized = true;
            }
        }

        public LightingMode EffectiveMode(bool gameRunning)
        { return gameRunning ? InGameMode : IdleMode; }

        public void RememberAnimation()
        {
            if (AnimationPreferences == null) AnimationPreferences = new List<AnimationPreference>();
            AnimationPreference entry = AnimationPreferences.FirstOrDefault(p => p.Effect == AnimationEffect);
            if (entry == null) { entry = new AnimationPreference { Effect = AnimationEffect }; AnimationPreferences.Add(entry); }
            entry.Speed = AnimationSpeed; entry.Intensity = AnimationIntensity; entry.RandomPalette = AnimationRandomPalette;
        }

        public void SelectAnimation(AnimationEffect effect)
        {
            RememberAnimation();
            var entry = AnimationPreferences.FirstOrDefault(p => p.Effect == effect);
            AnimationEffect = effect;
            AnimationSpeed = entry != null ? entry.Speed : effect == AnimationEffect.Loading ? 136 : 128;
            AnimationIntensity = entry != null ? entry.Intensity : effect == AnimationEffect.Loading ? 91 : 128;
            AnimationRandomPalette = entry == null || entry.RandomPalette;
        }

        private void InitializeTelemetryEffects()
        {
            // Preserve the existing feature set and add the requested green flag.
            // Less universal flags remain opt-in because support varies by game.
            TelemetrySpotterEnabled = true; TelemetryYellowEnabled = true;
            TelemetryBlueEnabled = true; TelemetryGreenEnabled = true;
            TelemetryWhiteEnabled = false; TelemetryBlackEnabled = false;
            TelemetryOrangeEnabled = false; TelemetryCheckeredEnabled = false;
            TelemetryEffectsInitialized = true;
            InitializeDrivingEffects();
        }

        private void InitializeDrivingEffects()
        {
            TelemetryAbsEnabled = true; TelemetryTcEnabled = true;
            TelemetryWheelLockEnabled = true; TelemetryRpmEnabled = false;
            DrivingEffectsInitialized = true;
        }

        public Settings Clone()
        {
            Settings s = (Settings)MemberwiseClone();
            s.AnimationPreferences = AnimationPreferences == null ? null : AnimationPreferences.Select(p => p.Clone()).ToList();
            s.Displays = Displays.Select(d => d.Clone()).ToList();
            s.Zones = Zones.Select(z => z.Clone()).ToList();
            return s;
        }

        public void Validate()
        {
            Require(SchemaVersion == 1, "Version de configuration inconnue.");
            Require(Enum.IsDefined(typeof(SpotterColor), SpotterColor), "Couleur spotter invalide.");
            Require(AnimationPreferences == null || (AnimationPreferences.Count <= 4 &&
                AnimationPreferences.All(p => p != null && Enum.IsDefined(typeof(AnimationEffect), p.Effect) &&
                p.Speed >= 1 && p.Speed <= 255 && p.Intensity >= 1 && p.Intensity <= 255) &&
                AnimationPreferences.Select(p => p.Effect).Distinct().Count() == AnimationPreferences.Count), "Réglages d'animation invalides.");
            Require(Enum.IsDefined(typeof(LightingMode), Mode), "Mode inconnu.");
            Require(Enum.IsDefined(typeof(LightingMode), IdleMode) && Enum.IsDefined(typeof(LightingMode), InGameMode), "Mode repos/jeu inconnu.");
            Require(Enum.IsDefined(typeof(AnimationEffect), AnimationEffect), "Animation inconnue.");
            Require(AnimationSpeed >= 1 && AnimationSpeed <= 255, "Vitesse d'animation invalide.");
            Require(AnimationIntensity >= 1 && AnimationIntensity <= 255, "Intensité d'animation invalide.");
            Range(Brightness, 0, 1, "Luminosité");
            Range(Warmth, -1, 1, "Température visuelle");
            Range(Tint, -1, 1, "Teinte du blanc");
            Range(CurrentBudgetAmps, .060, 15, "Budget estimé du ruban (A)");
            Require(LedStripCount == 3 || LedStripCount == 5, "Choisir 3 ou 5 bandes de 60 LED.");
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
