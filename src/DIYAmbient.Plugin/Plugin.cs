using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;
using DIYAmbient.Core;

[assembly: AssemblyTitle("DIY Ambient light EVO SimHub Plugin")]
[assembly: AssemblyDescription("Éclairage de cockpit par REALISTIC SIMCOCKPIT — version alpha")]
[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]

namespace DIYAmbient.Plugin
{
    [PluginName("DIY Ambient light EVO")]
    [PluginAuthor("REALISTIC SIMCOCKPIT")]
    [PluginDescription("Éclairage de cockpit Adalight, trois écrans, animations, profils de jeu et alertes — version alpha")]
    public sealed class AmbientPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        private volatile AmbientEngine engine;
        private SettingsControl view;
        private readonly TelemetryFlagReader flagReader = new TelemetryFlagReader();
        private readonly object settingsGate = new object();
        // Canonical settings are separate from temporary white preview settings.
        private Settings normalSettings;
        private string currentGameName = "";
        private string profileGameLoaded = "";
        public PluginManager PluginManager { get; set; }
        public ImageSource PictureIcon { get { return null; } }
        public string LeftMenuTitle { get { return "DIY Ambient light EVO"; } }
        internal AmbientEngine Engine { get { return engine; } }
        internal string StartupWarning { get; private set; }
        internal string CurrentGameName { get { return currentGameName; } }

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;
            if (engine != null) engine.Dispose();
            if (view != null) { view.StopTimer(); view = null; }
            string warning;
            Settings settings = Storage.Load(out warning);
            // The simplified UI always uses real output at the maximum software budget.
            if (!settings.StartEnabledPreferenceInitialized)
            { settings.StartEnabled = true; settings.StartEnabledPreferenceInitialized = true; }
            settings.PreviewOnly = false;
            settings.ElectricalConfirmed = true;
            settings.CurrentBudgetAmps = 15.0;
            Storage.Save(settings);
            StartupWarning = warning;
            normalSettings = settings.Clone();
            engine = new AmbientEngine(settings); // Always OFF at host startup / game reinitialization.
            if (settings.StartEnabled)
            {
                try { engine.SetEnabled(true); }
                catch (Exception ex) { Storage.Log("Saved startup enable rejected: " + ex.Message); }
            }
            Storage.Log("DIY Ambient light EVO 0.2.1-alpha initialized; output=" + (engine.State.Enabled ? "ON" : "OFF"));
            this.AttachDelegate("OutputEnabled", () => { AmbientEngine current = engine; return current != null && current.State.Enabled; });
            this.AttachDelegate("Status", () => { AmbientEngine current = engine; return current == null ? "Arrêté" : current.Status; });
            this.AttachDelegate("EstimatedAmps", () => { AmbientEngine current = engine; return current == null ? 0.0 : current.LastFrame.EstimatedAmps; });
            this.AddAction("Off", (a, b) => SafeAction(() => SetOutputEnabled(false)));
            this.AddAction("Toggle", (a, b) => SafeAction(() => SetOutputEnabled(!engine.State.Enabled)));
            this.AddAction("White", (a, b) => SafeAction(() => SetMode(LightingMode.White)));
            this.AddAction("Screen", (a, b) => SafeAction(() => SetMode(LightingMode.Screen)));
        }

        private void SetMode(LightingMode mode)
        { Settings s = GetSettings(); s.Mode = mode; ApplySettings(s, true); }
        private static void SafeAction(Action action)
        { try { action(); } catch (Exception ex) { Storage.Log("Action rejected: " + ex.Message); } }
        internal Settings GetSettings()
        { lock (settingsGate) return normalSettings.Clone(); }
        internal void PersistSettings()
        { lock (settingsGate) Storage.Save(normalSettings); }
        internal void ApplySettings(Settings settings, bool persist)
        {
            Settings copy = settings.Clone(); copy.Validate();
            lock (settingsGate)
            {
                // Disarm atomically in the engine before applying a changed port/budget.
                // Do not silently persist a configuration the engine has rejected.
                AmbientEngine current = engine;
                if (current == null) throw new InvalidOperationException("Le plugin est arrêté.");
                current.Apply(copy);
                normalSettings = copy;
                if (persist) Storage.Save(normalSettings);
            }
        }
        internal void SetOutputEnabled(bool enabled)
        {
            AmbientEngine current = engine;
            if (current == null) throw new InvalidOperationException("Le plugin est arrêté.");
            current.SetEnabled(enabled);
            Settings s = GetSettings(); s.StartEnabled = enabled; ApplySettings(s, true);
        }
        internal string[] ProfileNames() { return Profiles.Names(); }
        internal void SaveProfile(string name) { Profiles.Save(name, GetSettings()); }
        internal bool LoadProfile(string name)
        {
            Settings profile;
            if (!Profiles.TryLoad(name, out profile)) return false;
            ApplyProfile(profile); return true;
        }
        private void ApplyProfile(Settings profile)
        {
            Settings s = GetSettings();
            s.Mode = profile.Mode; s.Brightness = profile.Brightness; s.TelemetryLedCount = profile.TelemetryLedCount;
            s.SolidR = profile.SolidR; s.SolidG = profile.SolidG; s.SolidB = profile.SolidB;
            s.AnimationEffect = profile.AnimationEffect; s.AnimationSpeed = profile.AnimationSpeed;
            s.AnimationIntensity = profile.AnimationIntensity; s.AnimationRandomPalette = profile.AnimationRandomPalette;
            s.Warmth = profile.Warmth; s.Tint = profile.Tint;
            s.Displays = profile.Displays.Select(d => d.Clone()).ToList();
            s.Zones = profile.Zones.Select(z => z.Clone()).ToList();
            ApplySettings(s, true);
        }
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            AmbientEngine current = engine;
            if (current == null) return;
            try
            {
                string gameName = data.GameRunning ? (data.GameName ?? "").Trim() : "";
                currentGameName = gameName;
                if (gameName.Length > 0 && !string.Equals(profileGameLoaded, gameName, StringComparison.OrdinalIgnoreCase))
                {
                    profileGameLoaded = gameName;
                    Settings automatic;
                    if (Profiles.TryLoad(gameName, out automatic)) ApplyProfile(automatic);
                }
                else if (gameName.Length == 0) profileGameLoaded = "";
                object normalized = data.NewData;
                if (!data.GameRunning || normalized == null || flagReader.Read(data, "GamePaused", out _pauseAvailable))
                {
                    current.SetTelemetry(TelemetrySnapshot.Empty, "Pas de session active"); return;
                }
                bool leftAvailable, rightAvailable, yellowAvailable, blueAvailable;
                // Probe normalized public fields only. Unsupported properties stay INACTIVE;
                // no raw game-state bitmasks, no invented cross-game spotter support.
                bool left = flagReader.Read(normalized, "SpotterCarLeft", out leftAvailable);
                bool right = flagReader.Read(normalized, "SpotterCarRight", out rightAvailable);
                bool yellow = flagReader.Read(normalized, "Flag_Yellow", out yellowAvailable);
                bool blue = flagReader.Read(normalized, "Flag_Blue", out blueAvailable);
                string description = "Drapeaux : " + (yellowAvailable || blueAvailable ? "champs détectés" : "indisponibles") +
                    " | Spotter : " + (leftAvailable && rightAvailable ? "champs détectés (à valider en piste)" : "non exposé");
                current.SetTelemetry(new TelemetrySnapshot(true, left, right, yellow, blue, DateTime.UtcNow), description);
            }
            catch (Exception ex)
            {
                // Never throw on SimHub's game-update path.
                current.SetTelemetry(TelemetrySnapshot.Empty, "Lecture télémétrie indisponible : " + ex.Message);
            }
        }
        private bool _pauseAvailable;

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        { if (view == null) view = new SettingsControl(this); return view; }
        public void End(PluginManager pluginManager)
        {
            SettingsControl previousView = view; view = null;
            if (previousView != null) previousView.StopTimer();
            AmbientEngine previous = engine; engine = null;
            if (previous != null) previous.Dispose();
            try { PersistSettings(); }
            catch (Exception ex) { Storage.Log("Final settings save: " + ex.Message); }
        }
    }
}
