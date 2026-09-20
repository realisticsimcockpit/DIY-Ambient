using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal sealed class SettingsControl : UserControl
    {
        private readonly AmbientPlugin plugin;
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer saveTimer;
        private readonly CheckBox enabled;
        private readonly ComboBox mode;
        private readonly Slider brightness, alerts, warmth, tint;
        private readonly Slider animationSpeed, animationIntensity;
        private readonly TextBlock brightnessLabel, alertsLabel, animationSpeedLabel, animationIntensityLabel;
        private readonly StackPanel solidPalette, whiteControls, animationControls;
        private readonly ComboBox animationEffect;
        private readonly ComboBox port;
        private readonly ComboBox stripCount;
        private readonly ComboBox profileName;
        private readonly TextBlock activeGameLabel;
        private readonly CheckBox keepOnExit;
        private readonly ComboBox[] monitors = new ComboBox[3];
        private readonly TextBox[] first = new TextBox[3], last = new TextBox[3];
        private Settings installationWorking;
        private bool loading;
        private bool stopped;

        internal SettingsControl(AmbientPlugin owner)
        {
            plugin = owner;
            Language = XmlLanguage.GetLanguage("fr-FR");
            var root = new StackPanel { Margin = new Thickness(24), MaxWidth = 920, HorizontalAlignment = HorizontalAlignment.Left };
            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(new TextBlock { Text = "DIY Ambient light EVO by REALISTIC SIMCOCKPIT", FontSize = 30, FontWeight = FontWeights.SemiBold });
            var channel = new TextBlock { Margin = new Thickness(0, 5, 0, 2) };
            var channelLink = new Hyperlink(new Run("youtube.com/@realisticsimcockpit"))
            {
                NavigateUri = new Uri("https://www.youtube.com/@realisticsimcockpit")
            };
            channelLink.RequestNavigate += OpenExternalLink;
            channel.Inlines.Add(channelLink);
            root.Children.Add(channel);
            root.Children.Add(Note("Éclairage du cockpit • 60 LED • alpha 0.2.0"));
            if (!string.IsNullOrEmpty(plugin.StartupWarning)) root.Children.Add(Note(plugin.StartupWarning));
            enabled = new CheckBox { Content = "Éclairage activé", Margin = new Thickness(0, 18, 0, 12), FontSize = 17 };
            root.Children.Add(enabled);
            installationWorking = plugin.GetSettings();
            var connection = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            connection.Children.Add(new TextBlock { Text = "Port", Width = 42, VerticalAlignment = VerticalAlignment.Center });
            port = new ComboBox { IsEditable = true, Width = 120, Text = installationWorking.SerialPort, Margin = new Thickness(0, 0, 18, 0) };
            foreach (string name in SerialPort.GetPortNames().OrderBy(n => n)) port.Items.Add(name);
            connection.Children.Add(port);
            connection.Children.Add(new TextBlock { Text = "Bandes LED", Width = 82, VerticalAlignment = VerticalAlignment.Center });
            stripCount = new ComboBox { Width = 190 };
            stripCount.Items.Add("3 × 60 LED — 180 LED");
            stripCount.Items.Add("5 × 60 LED — 300 LED");
            connection.Children.Add(stripCount);
            root.Children.Add(connection);
            keepOnExit = new CheckBox { Content = "Garder la dernière couleur après fermeture de SimHub / arrêt du PC",
                IsChecked = installationWorking.KeepOnAfterExit,
                Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(keepOnExit);
            mode = new ComboBox { Width = 390, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
            mode.Items.Add("Blanc fixe"); mode.Items.Add("Couleur fixe"); mode.Items.Add("Image des 3 écrans — SDR expérimental"); mode.Items.Add("Animations inspirées de WLED");
            root.Children.Add(mode);
            solidPalette = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            solidPalette.Children.Add(Note("Palette de couleur fixe"));
            var palette = new WrapPanel { MaxWidth = 620, HorizontalAlignment = HorizontalAlignment.Left };
            AddSwatch(palette, "Blanc", 255, 255, 255);
            AddSwatch(palette, "Blanc chaud", 255, 180, 90);
            AddSwatch(palette, "Rouge", 255, 0, 0);
            AddSwatch(palette, "Orange", 255, 96, 0);
            AddSwatch(palette, "Jaune", 255, 220, 0);
            AddSwatch(palette, "Vert", 0, 210, 70);
            AddSwatch(palette, "Turquoise", 0, 210, 190);
            AddSwatch(palette, "Bleu", 0, 90, 255);
            AddSwatch(palette, "Violet", 125, 55, 255);
            AddSwatch(palette, "Rose", 255, 40, 150);
            solidPalette.Children.Add(palette);
            root.Children.Add(solidPalette);
            whiteControls = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            whiteControls.Children.Add(Note("Blanc : froid ← → chaud"));
            warmth = new Slider { Minimum = -1, Maximum = 1, TickFrequency = .02, IsSnapToTickEnabled = true, Width = 520 };
            whiteControls.Children.Add(warmth);
            whiteControls.Children.Add(Note("Teinte : moins vert ← → plus vert"));
            tint = new Slider { Minimum = -1, Maximum = 1, TickFrequency = .02, IsSnapToTickEnabled = true, Width = 520 };
            whiteControls.Children.Add(tint);
            root.Children.Add(whiteControls);
            animationControls = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            animationControls.Children.Add(Note("Animation 1D compatible avec les 60 LED"));
            animationEffect = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (string name in new[] { "Blink", "Breathe", "Wipe", "Scan", "Colorloop", "Rainbow", "Theater", "Chase", "Twinkle", "Fire Flicker" })
                animationEffect.Items.Add(name);
            animationControls.Children.Add(animationEffect);
            animationSpeedLabel = Note(""); animationControls.Children.Add(animationSpeedLabel);
            animationSpeed = new Slider { Minimum = 1, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 520 };
            animationControls.Children.Add(animationSpeed);
            animationIntensityLabel = Note(""); animationControls.Children.Add(animationIntensityLabel);
            animationIntensity = new Slider { Minimum = 1, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 520 };
            animationControls.Children.Add(animationIntensity);
            root.Children.Add(animationControls);
            brightnessLabel = Note(""); root.Children.Add(brightnessLabel);
            brightness = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 520 };
            root.Children.Add(brightness);
            alertsLabel = Note(""); root.Children.Add(alertsLabel);
            alerts = new Slider { Minimum = 0, Maximum = 60, TickFrequency = 2, IsSnapToTickEnabled = true, Width = 520 };
            root.Children.Add(alerts);
            root.Children.Add(Note("0 désactive les alertes. Hors alerte, toutes les LED retrouvent le fond choisi."));
            root.Children.Add(Button("Tester drapeau jaune · 3 s", () => plugin.Engine.TestYellowFlag()));
            root.Children.Add(new TextBlock { Text = "Écrans", FontSize = 18, Margin = new Thickness(0, 14, 0, 4) });
            MonitorInfo[] available = MonitorInfo.Enumerate();
            string[] roles = { "Gauche", "Centre", "Droite" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                DisplayMap map = installationWorking.Displays.Single(d => d.Position == i - 1);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
                row.Children.Add(new TextBlock { Text = roles[i], Width = 65, VerticalAlignment = VerticalAlignment.Center });
                monitors[i] = new ComboBox { Width = 245, Margin = new Thickness(0, 0, 8, 0) };
                foreach (MonitorInfo monitor in available) monitors[i].Items.Add(monitor);
                MonitorInfo selected = available.FirstOrDefault(m => string.Equals(m.DeviceName, map.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (selected == null) { selected = new MonitorInfo { DeviceName = map.DeviceName, Bounds = new System.Drawing.Rectangle() }; monitors[i].Items.Add(selected); }
                monitors[i].SelectedItem = selected; row.Children.Add(monitors[i]);
                first[i] = new TextBox { Width = 40, Text = map.FirstLed.ToString(), Margin = new Thickness(0, 0, 4, 0) };
                last[i] = new TextBox { Width = 40, Text = map.LastLed.ToString(), Margin = new Thickness(4, 0, 8, 0) };
                row.Children.Add(first[i]); row.Children.Add(new TextBlock { Text = "à", VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(last[i]);
                row.Children.Add(Button("Configurer les zones", () => EditZones(index)));
                root.Children.Add(row);
            }
            root.Children.Add(Button("Enregistrer la connexion et les écrans", SaveInstallation));
            root.Children.Add(new TextBlock { Text = "Profils de jeu", FontSize = 18, Margin = new Thickness(0, 14, 0, 4) });
            activeGameLabel = Note(""); root.Children.Add(activeGameLabel);
            profileName = new ComboBox { IsEditable = true, Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
            root.Children.Add(profileName);
            var profileButtons = new StackPanel { Orientation = Orientation.Horizontal };
            profileButtons.Children.Add(Button("Enregistrer", SaveProfile));
            profileButtons.Children.Add(Button("Charger", LoadProfile));
            profileButtons.Children.Add(Button("Nom du jeu actif", UseActiveGameName));
            root.Children.Add(profileButtons);
            RefreshProfileNames();

            LoadControls();
            enabled.Click += (s, e) => Guard(() => { plugin.SetOutputEnabled(enabled.IsChecked == true); Refresh(); });
            mode.SelectionChanged += (s, e) => Changed();
            brightness.ValueChanged += (s, e) => Changed();
            alerts.ValueChanged += (s, e) => Changed();
            warmth.ValueChanged += (s, e) => Changed();
            tint.ValueChanged += (s, e) => Changed();
            animationEffect.SelectionChanged += (s, e) => Changed();
            animationSpeed.ValueChanged += (s, e) => Changed();
            animationIntensity.ValueChanged += (s, e) => Changed();
            keepOnExit.Click += (s, e) => Changed();
            stripCount.SelectionChanged += (s, e) => Changed();
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); Guard(() => plugin.PersistSettings()); };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (s, e) =>
            {
                try { Refresh(); }
                catch (Exception ex) { timer.Stop(); Storage.Log("Settings refresh stopped: " + ex.Message); }
            };
            Loaded += (s, e) => { if (!stopped) timer.Start(); };
            Unloaded += (s, e) => { timer.Stop(); saveTimer.Stop(); if (!stopped && plugin.Engine != null) Guard(() => plugin.PersistSettings()); };
            Refresh();
        }

        internal static TextBlock Note(string text)
        { return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 7), MaxWidth = 840 }; }
        internal static Button Button(string text, Action action)
        {
            var button = new Button { Content = text, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += (s, e) => Guard(action); return button;
        }
        internal static void Guard(Action action)
        { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "DIY Ambient light EVO", MessageBoxButton.OK, MessageBoxImage.Warning); } }

        private static void OpenExternalLink(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "DIY Ambient light EVO", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddSwatch(Panel palette, string name, byte r, byte g, byte b)
        {
            var color = Color.FromRgb(r, g, b);
            var swatch = new Button { Width = 38, Height = 34, Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(0), Background = new SolidColorBrush(color), BorderBrush = Brushes.DimGray,
                BorderThickness = new Thickness(1), ToolTip = name };
            swatch.Click += (s, e) => Guard(() => ApplySolidColor(r, g, b));
            palette.Children.Add(swatch);
        }

        private void ApplySolidColor(byte r, byte g, byte b)
        {
            Settings s = plugin.GetSettings();
            s.SolidR = r; s.SolidG = g; s.SolidB = b;
            if (s.Mode != LightingMode.Animation) s.Mode = LightingMode.Solid;
            plugin.ApplySettings(s, true); LoadControls();
        }

        private void LoadControls()
        {
            if (stopped || plugin.Engine == null) return;
            loading = true;
            Settings s = plugin.GetSettings();
            enabled.IsChecked = plugin.Engine.State.Enabled;
            mode.SelectedIndex = (int)s.Mode; brightness.Value = s.Brightness * 100; alerts.Value = s.TelemetryLedCount;
            warmth.Value = s.Warmth; tint.Value = s.Tint;
            animationEffect.SelectedIndex = (int)s.AnimationEffect;
            animationSpeed.Value = s.AnimationSpeed; animationIntensity.Value = s.AnimationIntensity;
            keepOnExit.IsChecked = s.KeepOnAfterExit;
            stripCount.SelectedIndex = s.LedStripCount == 5 ? 1 : 0;
            solidPalette.Visibility = s.Mode == LightingMode.Solid || s.Mode == LightingMode.Animation ? Visibility.Visible : Visibility.Collapsed;
            whiteControls.Visibility = s.Mode == LightingMode.White ? Visibility.Visible : Visibility.Collapsed;
            animationControls.Visibility = s.Mode == LightingMode.Animation ? Visibility.Visible : Visibility.Collapsed;
            loading = false; Labels();
        }
        private void Labels()
        {
            brightnessLabel.Text = "Luminosité : " + ((int)brightness.Value) + " % du maximum configuré";
            int n = (int)alerts.Value;
            alertsLabel.Text = n == 0 ? "LED de télémétrie : désactivées" : string.Format("LED de télémétrie : {0} — {1} à gauche + {1} à droite", n, n / 2);
            animationSpeedLabel.Text = "Vitesse de l'animation : " + ((int)animationSpeed.Value);
            animationIntensityLabel.Text = "Intensité / largeur : " + ((int)animationIntensity.Value);
        }
        private void Changed()
        {
            if (loading || stopped || mode.SelectedIndex < 0) return;
            Guard(() =>
            {
                Settings s = plugin.GetSettings(); s.Mode = (LightingMode)mode.SelectedIndex;
                s.Brightness = brightness.Value / 100; s.TelemetryLedCount = (int)alerts.Value / 2 * 2;
                s.Warmth = warmth.Value; s.Tint = tint.Value;
                s.AnimationEffect = (AnimationEffect)Math.Max(0, animationEffect.SelectedIndex);
                s.AnimationSpeed = (int)animationSpeed.Value; s.AnimationIntensity = (int)animationIntensity.Value;
                s.KeepOnAfterExit = keepOnExit.IsChecked == true;
                s.LedStripCount = stripCount.SelectedIndex == 1 ? 5 : 3;
                plugin.ApplySettings(s, false); saveTimer.Stop(); saveTimer.Start(); Labels();
                solidPalette.Visibility = s.Mode == LightingMode.Solid || s.Mode == LightingMode.Animation ? Visibility.Visible : Visibility.Collapsed;
                whiteControls.Visibility = s.Mode == LightingMode.White ? Visibility.Visible : Visibility.Collapsed;
                animationControls.Visibility = s.Mode == LightingMode.Animation ? Visibility.Visible : Visibility.Collapsed;
            });
        }
        private void Refresh()
        {
            AmbientEngine current = plugin.Engine;
            if (current == null || stopped) return;
            // Actions/Stream Deck may change a mode while this page is open.
            // Keep the controls in sync without firing their change handlers.
            Settings canonical = plugin.GetSettings();
            if (mode.SelectedIndex != (int)canonical.Mode || Math.Abs(brightness.Value - canonical.Brightness * 100) > .001 ||
                alerts.Value != canonical.TelemetryLedCount || Math.Abs(warmth.Value - canonical.Warmth) > .001 ||
                Math.Abs(tint.Value - canonical.Tint) > .001 || keepOnExit.IsChecked != canonical.KeepOnAfterExit ||
                animationEffect.SelectedIndex != (int)canonical.AnimationEffect || animationSpeed.Value != canonical.AnimationSpeed ||
                animationIntensity.Value != canonical.AnimationIntensity ||
                stripCount.SelectedIndex != (canonical.LedStripCount == 5 ? 1 : 0))
                LoadControls();
            enabled.IsChecked = current.State.Enabled;
            activeGameLabel.Text = string.IsNullOrWhiteSpace(plugin.CurrentGameName) ? "Jeu actif : aucun" : "Jeu actif : " + plugin.CurrentGameName;
        }
        private Settings ReadInstallation()
        {
            Settings next = installationWorking.Clone();
            next.PreviewOnly = false;
            next.ElectricalConfirmed = true;
            next.KeepOnAfterExit = keepOnExit.IsChecked == true;
            next.LedStripCount = stripCount.SelectedIndex == 1 ? 5 : 3;
            next.SerialPort = port.Text.Trim();
            next.CurrentBudgetAmps = 15.0;
            var maps = new List<DisplayMap>(); var zones = new List<LedZone>();
            for (int i = 0; i < 3; i++)
            {
                MonitorInfo display = monitors[i].SelectedItem as MonitorInfo;
                int lo, hi;
                if (display == null || !int.TryParse(first[i].Text, out lo) || !int.TryParse(last[i].Text, out hi) || lo < 1 || hi > 60 || lo > hi)
                    throw new ArgumentException("Écran ou plage invalide.");
                var map = new DisplayMap { DeviceName = display.DeviceName, Position = i - 1, FirstLed = lo, LastLed = hi };
                maps.Add(map);
                foreach (LedZone fallback in Settings.DefaultZones(map))
                {
                    LedZone existing = next.Zones.FirstOrDefault(z => z.Led == fallback.Led);
                    LedZone zone = existing == null ? fallback : existing.Clone();
                    zone.DeviceName = map.DeviceName; zones.Add(zone);
                }
            }
            next.Displays = maps; next.Zones = zones; next.Validate(); return next;
        }
        private void SaveInstallation()
        {
            installationWorking = ReadInstallation();
            plugin.ApplySettings(installationWorking, true);
            LoadControls();
        }
        private void EditZones(int position)
        {
            installationWorking = ReadInstallation();
            DisplayMap map = installationWorking.Displays.Single(d => d.Position == position - 1);
            MonitorInfo monitor = MonitorInfo.Enumerate().FirstOrDefault(m => string.Equals(m.DeviceName, map.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (monitor == null) throw new InvalidOperationException("Cet écran n'est pas connecté.");
            plugin.Engine.SetEditing(true);
            try { new ZoneEditorWindow(installationWorking, monitor).ShowDialog(); }
            finally { plugin.Engine.SetEditing(false); }
            plugin.ApplySettings(installationWorking, true);
        }
        private void RefreshProfileNames()
        {
            string text = profileName == null ? "" : profileName.Text;
            if (profileName == null) return;
            profileName.Items.Clear();
            foreach (string name in plugin.ProfileNames()) profileName.Items.Add(name);
            profileName.Text = text;
        }
        private void UseActiveGameName()
        {
            if (string.IsNullOrWhiteSpace(plugin.CurrentGameName)) throw new InvalidOperationException("Aucun jeu actif dans SimHub.");
            profileName.Text = plugin.CurrentGameName;
        }
        private void SaveProfile()
        {
            string name = string.IsNullOrWhiteSpace(profileName.Text) ? plugin.CurrentGameName : profileName.Text;
            plugin.SaveProfile(name); profileName.Text = name; RefreshProfileNames();
        }
        private void LoadProfile()
        {
            if (!plugin.LoadProfile(profileName.Text)) throw new InvalidOperationException("Profil introuvable.");
            installationWorking = plugin.GetSettings(); LoadControls();
        }
        internal void StopTimer()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Action stop = () =>
            {
                stopped = true; timer.Stop(); saveTimer.Stop();
            };
            try
            {
                if (Dispatcher.CheckAccess()) stop();
                else Dispatcher.BeginInvoke(stop);
            }
            catch (InvalidOperationException) { /* Host dispatcher already shutting down. */ }
        }
    }
}
