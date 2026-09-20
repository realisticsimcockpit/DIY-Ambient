using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal sealed class ConfigurationWindow : Window
    {
        private readonly AmbientPlugin plugin;
        private Settings working;
        private readonly ComboBox port;
        private readonly CheckBox preview, confirmed;
        private readonly TextBox budget;
        private readonly ComboBox[] monitors = new ComboBox[3];
        private readonly TextBox[] first = new TextBox[3], last = new TextBox[3];

        internal ConfigurationWindow(AmbientPlugin owner)
        {
            plugin = owner; working = owner.GetSettings();
            Title = "DIY-Ambient — installation"; Width = 860; Height = 730;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var panel = new StackPanel { Margin = new Thickness(22) };
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(new TextBlock { Text = "Configuration initiale", FontSize = 25 });
            panel.Children.Add(SettingsControl.Note("Le firmware reste inchangé : 60 LED, RGB, 115200 bauds. Fermer Prismatik et désactiver les autres sorties SimHub utilisant le même port."));
            preview = new CheckBox { Content = "Aperçu sans matériel (aucun port ouvert)", IsChecked = working.PreviewOnly, Margin = new Thickness(0, 8, 0, 8) };
            panel.Children.Add(preview);
            panel.Children.Add(SettingsControl.Note("Port du contrôleur"));
            port = new ComboBox { IsEditable = true, Width = 170, HorizontalAlignment = HorizontalAlignment.Left, Text = working.SerialPort };
            foreach (string name in SerialPort.GetPortNames().OrderBy(n => n)) port.Items.Add(name);
            panel.Children.Add(port);
            panel.Children.Add(SettingsControl.Note("Budget de courant autorisé pour le ruban (A) — estimation, pas mesure"));
            budget = new TextBox { Text = working.CurrentBudgetAmps.ToString(CultureInfo.CurrentCulture), Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(budget);
            confirmed = new CheckBox { Content = "Alimentation et câblage vérifiés pour ce budget", IsChecked = working.ElectricalConfirmed, Margin = new Thickness(0, 8, 0, 8) };
            panel.Children.Add(confirmed);
            // An old confirmation does not authorize a new port or a larger budget.
            budget.TextChanged += (s, e) => confirmed.IsChecked = false;
            port.SelectionChanged += (s, e) => confirmed.IsChecked = false;
            port.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => confirmed.IsChecked = false));
            panel.Children.Add(SettingsControl.Note("Le plafond logiciel ne protège pas d'un court-circuit. Les flashes RGB de démarrage de ton firmware échappent au PC. Ne pas valider un budget sans vérifier le montage. 0,5 A est seulement la valeur provisoire de développement."));
            panel.Children.Add(new TextBlock { Text = "Écrans et plages de LED", FontSize = 20, Margin = new Thickness(0, 15, 0, 5) });
            MonitorInfo[] available = MonitorInfo.Enumerate();
            string[] roles = { "Gauche", "Centre", "Droite" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                DisplayMap map = working.Displays.Single(d => d.Position == i - 1);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 7) };
                row.Children.Add(new TextBlock { Text = roles[i], Width = 65, VerticalAlignment = VerticalAlignment.Center });
                monitors[i] = new ComboBox { Width = 265, Margin = new Thickness(0, 0, 8, 0) };
                foreach (MonitorInfo monitor in available) monitors[i].Items.Add(monitor);
                MonitorInfo selected = available.FirstOrDefault(m => string.Equals(m.DeviceName, map.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (selected == null)
                {
                    selected = new MonitorInfo { DeviceName = map.DeviceName, Bounds = new System.Drawing.Rectangle() };
                    monitors[i].Items.Add(selected);
                }
                monitors[i].SelectedItem = selected; row.Children.Add(monitors[i]);
                first[i] = new TextBox { Width = 40, Text = map.FirstLed.ToString(), Margin = new Thickness(0, 0, 4, 0) };
                last[i] = new TextBox { Width = 40, Text = map.LastLed.ToString(), Margin = new Thickness(4, 0, 8, 0) };
                row.Children.Add(first[i]); row.Children.Add(new TextBlock { Text = "à", VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(last[i]);
                row.Children.Add(SettingsControl.Button("Placer les zones", () => EditZones(index)));
                panel.Children.Add(row);
            }
            panel.Children.Add(SettingsControl.Note("Valeurs initiales : DISPLAY2 = 1–20 ; DISPLAY1 (centre) = 21–40 ; DISPLAY3 = 41–60. Vérifier les noms réels. Les rôles gauche/centre/droite servent au partage automatique des alertes."));
            panel.Children.Add(SettingsControl.Button("Enregistrer l'installation", Save));
            panel.Children.Add(SettingsControl.Button("Annuler", () => Close()));
        }

        private Settings ReadAll()
        {
            Settings next = working.Clone(); next.PreviewOnly = preview.IsChecked == true;
            next.ElectricalConfirmed = confirmed.IsChecked == true; next.SerialPort = port.Text.Trim();
            double amps;
            if (!double.TryParse(budget.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out amps) &&
                !double.TryParse(budget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out amps))
                throw new ArgumentException("Budget de courant invalide.");
            next.CurrentBudgetAmps = amps;
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
        private void EditZones(int position)
        {
            working = ReadAll();
            DisplayMap map = working.Displays.Single(d => d.Position == position - 1);
            MonitorInfo monitor = MonitorInfo.Enumerate().FirstOrDefault(m => string.Equals(m.DeviceName, map.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (monitor == null) throw new InvalidOperationException("Cet écran n'est pas connecté.");
            new ZoneEditorWindow(working, monitor, plugin.Engine) { Owner = this }.ShowDialog();
        }
        private void Save()
        {
            working = ReadAll();
            // Apply performs route/budget changes and disarms in one state transition.
            plugin.ApplySettings(working, true); Close();
        }
    }
}
