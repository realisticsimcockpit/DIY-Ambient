using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DIYAmbient.Core;
using Forms = System.Windows.Forms;

namespace DIYAmbient.Plugin
{
    internal sealed class SettingsControl : UserControl
    {
        private readonly AmbientPlugin plugin;
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer saveTimer;
        private readonly CheckBox enabled;
        private readonly ComboBox mode;
        private readonly Slider brightness, alerts;
        private readonly TextBlock brightnessLabel, alertsLabel, status, details;
        private readonly Rectangle[] pixels = new Rectangle[60];
        private bool loading;
        private bool stopped;
        private Window modal;

        internal SettingsControl(AmbientPlugin owner)
        {
            plugin = owner;
            var root = new StackPanel { Margin = new Thickness(24), MaxWidth = 920, HorizontalAlignment = HorizontalAlignment.Left };
            Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            root.Children.Add(new TextBlock { Text = "DIY-Ambient", FontSize = 30, FontWeight = FontWeights.SemiBold });
            root.Children.Add(Note("Éclairage du cockpit • 60 LED • alpha 0.1.1 — non validée sur matériel"));
            if (!string.IsNullOrEmpty(plugin.StartupWarning)) root.Children.Add(Note(plugin.StartupWarning));
            enabled = new CheckBox { Content = "Éclairage activé", Margin = new Thickness(0, 18, 0, 12), FontSize = 17 };
            root.Children.Add(enabled);
            mode = new ComboBox { Width = 390, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
            mode.Items.Add("Blanc fixe"); mode.Items.Add("Couleur fixe"); mode.Items.Add("Image des 3 écrans — SDR expérimental");
            root.Children.Add(mode);
            var choices = new StackPanel { Orientation = Orientation.Horizontal };
            choices.Children.Add(Button("Choisir une couleur", ChooseColor));
            choices.Children.Add(Button("Ajuster mon blanc", AdjustWhite));
            root.Children.Add(choices);
            brightnessLabel = Note(""); root.Children.Add(brightnessLabel);
            brightness = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 520 };
            root.Children.Add(brightness);
            alertsLabel = Note(""); root.Children.Add(alertsLabel);
            alerts = new Slider { Minimum = 0, Maximum = 60, TickFrequency = 2, IsSnapToTickEnabled = true, Width = 520 };
            root.Children.Add(alerts);
            root.Children.Add(Note("0 désactive les alertes. Hors alerte, toutes les LED retrouvent le fond choisi."));
            var testRow = new StackPanel { Orientation = Orientation.Horizontal };
            testRow.Children.Add(Button("Tester gauche · 3 s", () => plugin.Engine.Test(true, false, 0)));
            testRow.Children.Add(Button("Tester droite · 3 s", () => plugin.Engine.Test(false, true, 0)));
            testRow.Children.Add(Button("Tester les deux", () => plugin.Engine.Test(true, true, 0)));
            root.Children.Add(testRow);
            var strip = new WrapPanel { Width = 720, Margin = new Thickness(0, 14, 0, 12) };
            for (int i = 0; i < 60; i++)
            {
                int id = i + 1;
                var cell = new StackPanel { Margin = new Thickness(1) };
                pixels[i] = new Rectangle { Width = 20, Height = 18, Fill = Brushes.Black, Stroke = Brushes.Gray, StrokeThickness = 1 };
                pixels[i].MouseLeftButtonDown += (s, e) => plugin.Engine.Test(false, false, id);
                cell.Children.Add(pixels[i]);
                cell.Children.Add(new TextBlock { Text = id.ToString(), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center });
                strip.Children.Add(cell);
            }
            root.Children.Add(strip);
            root.Children.Add(Note("Aperçu dans l'ordre physique 1 → 60. Cliquer une LED pour l'identifier (éclairage activé)."));
            status = Note(""); details = Note(""); root.Children.Add(status); root.Children.Add(details);
            root.Children.Add(Button("Configuration de l'installation", Configure));

            LoadControls();
            enabled.Click += (s, e) => Guard(() => { plugin.Engine.SetEnabled(enabled.IsChecked == true); Refresh(); });
            mode.SelectionChanged += (s, e) => Changed();
            brightness.ValueChanged += (s, e) => Changed();
            alerts.ValueChanged += (s, e) => Changed();
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
        { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "DIY-Ambient", MessageBoxButton.OK, MessageBoxImage.Warning); } }

        private void LoadControls()
        {
            if (stopped || plugin.Engine == null) return;
            loading = true;
            Settings s = plugin.GetSettings();
            enabled.IsChecked = plugin.Engine.State.Enabled;
            mode.SelectedIndex = (int)s.Mode; brightness.Value = s.Brightness * 100; alerts.Value = s.TelemetryLedCount;
            loading = false; Labels();
        }
        private void Labels()
        {
            brightnessLabel.Text = "Luminosité : " + ((int)brightness.Value) + " % du maximum configuré";
            int n = (int)alerts.Value;
            alertsLabel.Text = n == 0 ? "LED de télémétrie : désactivées" : string.Format("LED de télémétrie : {0} — {1} à gauche + {1} à droite", n, n / 2);
        }
        private void Changed()
        {
            if (loading || stopped || mode.SelectedIndex < 0) return;
            Guard(() =>
            {
                Settings s = plugin.GetSettings(); s.Mode = (LightingMode)mode.SelectedIndex;
                s.Brightness = brightness.Value / 100; s.TelemetryLedCount = (int)alerts.Value / 2 * 2;
                plugin.ApplySettings(s, false); saveTimer.Stop(); saveTimer.Start(); Labels();
            });
        }
        private void Refresh()
        {
            AmbientEngine current = plugin.Engine;
            if (current == null || stopped) return;
            // Actions/Stream Deck may change a mode while this page is open.
            // Keep the controls in sync without firing their change handlers.
            Settings canonical = plugin.GetSettings();
            if (modal == null && (mode.SelectedIndex != (int)canonical.Mode ||
                Math.Abs(brightness.Value - canonical.Brightness * 100) > .001 || alerts.Value != canonical.TelemetryLedCount))
                LoadControls();
            enabled.IsChecked = current.State.Enabled;
            FrameResult frame = current.LastFrame;
            for (int i = 0; i < pixels.Length; i++)
            {
                Rgb c = frame.Colors[i]; pixels[i].Fill = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            }
            status.Text = current.Status;
            details.Text = string.Format(CultureInfo.CurrentCulture, "Courant modélisé (non mesuré) : {0:F2} A — budget {1:F2} A{2}\n{3}\n{4}",
                frame.EstimatedAmps, current.State.Settings.CurrentBudgetAmps, frame.PowerScale < .999 ? " — maximum réduit selon le budget" : "",
                current.CaptureStatus, current.TelemetryStatus);
        }
        private void ChooseColor()
        {
            Settings s = plugin.GetSettings();
            using (var dialog = new Forms.ColorDialog())
            {
                dialog.Color = System.Drawing.Color.FromArgb(s.SolidR, s.SolidG, s.SolidB);
                if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
                s.SolidR = dialog.Color.R; s.SolidG = dialog.Color.G; s.SolidB = dialog.Color.B; s.Mode = LightingMode.Solid;
                plugin.ApplySettings(s, true); LoadControls();
            }
        }
        private void ShowModal(Window window)
        {
            Window owner = Window.GetWindow(this);
            if (owner != null && owner != window) window.Owner = owner;
            modal = window;
            try { window.ShowDialog(); }
            finally { modal = null; }
        }
        private void AdjustWhite()
        {
            saveTimer.Stop(); plugin.PersistSettings();
            Settings trial = plugin.GetSettings(); trial.Mode = LightingMode.White; trial.TelemetryLedCount = 0;
            plugin.Engine.ClearTest();
            var window = new Window { Title = "Ajuster mon blanc", Width = 520, Height = 390, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var panel = new StackPanel { Margin = new Thickness(22) }; window.Content = panel;
            panel.Children.Add(Note("Ajustement visuel, pas une calibration mesurée. Regarder la lumière réelle, pas l'aperçu écran. L'éclairage doit être activé. Les alertes sont suspendues pendant cet essai."));
            panel.Children.Add(Note("Plus froid ← → Plus chaud"));
            var warmth = new Slider { Minimum = -1, Maximum = 1, Value = trial.Warmth, TickFrequency = .02, IsSnapToTickEnabled = true }; panel.Children.Add(warmth);
            panel.Children.Add(Note("Moins vert ← → Plus vert"));
            var tint = new Slider { Minimum = -1, Maximum = 1, Value = trial.Tint, TickFrequency = .02, IsSnapToTickEnabled = true }; panel.Children.Add(tint);
            Action update = () => Guard(() =>
            {
                trial.Warmth = warmth.Value; trial.Tint = tint.Value;
                plugin.PreviewSettings(trial); // NEVER change the canonical saved mode/count.
            });
            warmth.ValueChanged += (a, b) => update(); tint.ValueChanged += (a, b) => update();
            panel.Children.Add(Button("Conserver ce blanc", () =>
            {
                Settings chosen = plugin.GetSettings();
                chosen.Warmth = trial.Warmth; chosen.Tint = trial.Tint;
                plugin.ApplySettings(chosen, true); window.Close();
            }));
            panel.Children.Add(Button("Annuler", () => window.Close()));
            plugin.PreviewSettings(trial);
            try { ShowModal(window); }
            finally
            {
                if (!stopped) { plugin.RestoreSettingsPreview(); LoadControls(); }
            }
        }
        private void Configure()
        {
            saveTimer.Stop(); plugin.PersistSettings();
            AmbientEngine current = plugin.Engine;
            current.SetEditing(true);
            try { ShowModal(new ConfigurationWindow(plugin)); }
            finally
            {
                if (!stopped && object.ReferenceEquals(plugin.Engine, current))
                { current.SetEditing(false); LoadControls(); }
            }
        }
        internal void StopTimer()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Action stop = () =>
            {
                stopped = true; timer.Stop(); saveTimer.Stop();
                if (modal != null) modal.Close();
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
