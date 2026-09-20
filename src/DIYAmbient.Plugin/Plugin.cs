using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;
using DIYAmbient.Core;

[assembly: AssemblyTitle("DIY-Ambient SimHub Plugin")]
[assembly: AssemblyDescription("Integrated cockpit lighting — development alpha")]
[assembly: AssemblyVersion("0.1.1.0")]
[assembly: AssemblyFileVersion("0.1.1.0")]

namespace DIYAmbient.Plugin
{
    [PluginName("DIY-Ambient")]
    [PluginAuthor("Realistic Simcockpit")]
    [PluginDescription("60 LED Adalight, trois écrans, blanc fixe et alertes — alpha de développement")]
    public sealed class AmbientPlugin : IPlugin, IDataPlugin, IWPFSettingsV2
    {
        private volatile AmbientEngine engine;
        private SettingsControl view;
        private readonly TelemetryFlagReader flagReader = new TelemetryFlagReader();
        private readonly object settingsGate = new object();
        // Canonical settings are separate from temporary white preview settings.
        private Settings normalSettings;
        public PluginManager PluginManager { get; set; }
        public ImageSource PictureIcon { get { return null; } }
        public string LeftMenuTitle { get { return "DIY-Ambient"; } }
        internal AmbientEngine Engine { get { return engine; } }
        internal string StartupWarning { get; private set; }

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;
            if (engine != null) engine.Dispose();
            if (view != null) { view.StopTimer(); view = null; }
            string warning;
            Settings settings = Storage.Load(out warning);
            StartupWarning = warning;
            normalSettings = settings.Clone();
            engine = new AmbientEngine(settings); // Always OFF at host startup / game reinitialization.
            Storage.Log("DIY-Ambient 0.1.1-alpha initialized; output OFF; preview=" + settings.PreviewOnly);
            this.AttachDelegate("OutputEnabled", () => { AmbientEngine current = engine; return current != null && current.State.Enabled; });
            this.AttachDelegate("Status", () => { AmbientEngine current = engine; return current == null ? "Arrêté" : current.Status; });
            this.AttachDelegate("EstimatedAmps", () => { AmbientEngine current = engine; return current == null ? 0.0 : current.LastFrame.EstimatedAmps; });
            this.AddAction("Off", (a, b) => SafeAction(() => engine.SetEnabled(false)));
            this.AddAction("Toggle", (a, b) => SafeAction(() => engine.SetEnabled(!engine.State.Enabled)));
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
        internal void PreviewSettings(Settings settings)
        {
            AmbientEngine current = engine;
            if (current == null) throw new InvalidOperationException("Le plugin est arrêté.");
            current.Apply(settings);
        }
        internal void RestoreSettingsPreview()
        {
            AmbientEngine current = engine;
            if (current != null) current.Apply(GetSettings());
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            AmbientEngine current = engine;
            if (current == null) return;
            try
            {
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
                    " | Spotter : " + (leftAvailable && rightAvailable ? "champs détectés (à valider en piste)" : "non exposé — tests manuels disponibles");
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
            // Never save a temporary calibration/test frame as the normal configuration.
            try { PersistSettings(); }
            catch (Exception ex) { Storage.Log("Final settings save: " + ex.Message); }
        }
    }
}
