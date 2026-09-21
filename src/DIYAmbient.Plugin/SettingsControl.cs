using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
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
        private readonly ComboBox idleMode, inGameMode;
        private readonly Slider brightness, alerts, warmth, tint;
        private readonly Slider animationSpeed, animationIntensity;
        private readonly TextBlock brightnessLabel, alertsLabel, animationSpeedLabel, animationIntensityLabel;
        private readonly StackPanel solidPalette, whiteControls, animationControls;
        private readonly ComboBox animationEffect;
        private readonly CheckBox randomPalette;
        private readonly CheckBox telemetrySpotter, telemetryYellow, telemetryBlue, telemetryGreen;
        private readonly CheckBox telemetryWhite;
        private readonly CheckBox telemetryAbs, telemetryTc, telemetryWheelLock;
        private readonly ComboBox telemetryTestEffect, spotterColor;
        private static readonly TelemetryEffect[] TestEffects = { TelemetryEffect.SpotterLeft, TelemetryEffect.SpotterRight, TelemetryEffect.Yellow, TelemetryEffect.Blue, TelemetryEffect.Green, TelemetryEffect.White, TelemetryEffect.Abs, TelemetryEffect.Tc, TelemetryEffect.WheelLock };
        private readonly ComboBox port;
        private readonly ComboBox stripCount;
        private readonly ComboBox profileName;
        private readonly TextBlock activeGameLabel;
        private readonly CheckBox keepOnExit;
        private readonly ComboBox[] monitors = new ComboBox[3];
        private readonly TextBox[] first = new TextBox[3], last = new TextBox[3];
        private static readonly AnimationEffect[] AnimationEffects = { AnimationEffect.Colorloop, AnimationEffect.Rainbow, AnimationEffect.FireFlicker, AnimationEffect.Loading };
        private Settings installationWorking;
        private bool loading;
        private bool stopped;

        internal SettingsControl(AmbientPlugin owner)
        {
            plugin = owner;
            Foreground = Brushes.White;
            ApplyDarkStyles();
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
            root.Children.Add(Note("Éclairage du cockpit • 60 LED • alpha 0.2.1"));
            if (!string.IsNullOrEmpty(plugin.StartupWarning)) root.Children.Add(Note(plugin.StartupWarning));
            enabled = new CheckBox { Content = "Éclairage activé", Margin = new Thickness(0, 18, 0, 12), FontSize = 17 };
            root.Children.Add(enabled);
            var installation = new StackPanel { Margin = new Thickness(12) };
            var personalization = new StackPanel { Margin = new Thickness(12) };
            var firmware = new StackPanel { Margin = new Thickness(12) };
            var tabs = new TabControl { Margin = new Thickness(0, 6, 0, 0) };
            tabs.Items.Add(new TabItem { Header = "Installation", Content = installation });
            tabs.Items.Add(new TabItem { Header = "Personnalisation", Content = personalization });
            tabs.Items.Add(new TabItem { Header = "Firmware", Content = firmware });
            tabs.SelectedIndex = 1;
            root.Children.Add(tabs);
            root = personalization;
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
            installation.Children.Add(connection);
            keepOnExit = new CheckBox { Content = "Garder la dernière couleur après fermeture de SimHub / arrêt du PC",
                IsChecked = installationWorking.KeepOnAfterExit,
                Margin = new Thickness(0, 0, 0, 8) };
            installation.Children.Add(keepOnExit);
            idleMode = ModeCombo("Mode au repos");
            inGameMode = ModeCombo("Mode en jeu");
            root.Children.Add(Note("Mode au repos — aucun jeu détecté")); root.Children.Add(idleMode);
            root.Children.Add(Note("Mode en jeu — SimHub détecte un jeu en cours")); root.Children.Add(inGameMode);
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
            animationControls.Children.Add(Note("Animation"));
            animationEffect = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (string name in new[] { "Colorloop", "Rainbow", "Fire Flicker", "Loading" })
                animationEffect.Items.Add(name);
            animationControls.Children.Add(animationEffect);
            randomPalette = new CheckBox { Content = "Cycle aléatoire des couleurs", Margin = new Thickness(0, 8, 0, 2) };
            animationControls.Children.Add(randomPalette);
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
            root.Children.Add(Note("Effets de télémétrie actifs"));
            var telemetryChoices = new WrapPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
            telemetrySpotter = TelemetryChoice("Spotter gauche / droite", telemetryChoices);
            telemetryYellow = TelemetryChoice("Drapeau jaune", telemetryChoices);
            telemetryBlue = TelemetryChoice("Drapeau bleu", telemetryChoices);
            telemetryGreen = TelemetryChoice("Drapeau vert", telemetryChoices);
            telemetryWhite = TelemetryChoice("Drapeau blanc", telemetryChoices);
            telemetryAbs = TelemetryChoice("ABS actif", telemetryChoices);
            telemetryTc = TelemetryChoice("TC actif", telemetryChoices);
            telemetryWheelLock = TelemetryChoice("Blocage des roues", telemetryChoices);
            root.Children.Add(telemetryChoices);
            root.Children.Add(Note("Couleur du spotter"));
            spotterColor = new ComboBox { Width = 230, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (string name in new[] { "Rouge", "Orange", "Violet", "Rose", "Blanc" }) spotterColor.Items.Add(name);
            root.Children.Add(spotterColor);
            var telemetryTest = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 8) };
            telemetryTestEffect = new ComboBox { Width = 230, SelectedIndex = 2 };
            foreach (string name in new[] { "Spotter gauche", "Spotter droite", "Drapeau jaune", "Drapeau bleu", "Drapeau vert", "Drapeau blanc", "ABS actif", "TC actif", "Blocage des roues" })
                telemetryTestEffect.Items.Add(name);
            telemetryTest.Children.Add(telemetryTestEffect);
            telemetryTest.Children.Add(Button("Tester l'effet · 3 s", TestTelemetry));
            root.Children.Add(telemetryTest);
            installation.Children.Add(new TextBlock { Text = "Écrans", FontSize = 18, Margin = new Thickness(0, 14, 0, 4) });
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
                installation.Children.Add(row);
            }
            installation.Children.Add(Button("Enregistrer la connexion et les écrans", SaveInstallation));
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
            AddFirmwareControls(firmware);

            LoadControls();
            enabled.Click += (s, e) => Guard(() => { plugin.SetOutputEnabled(enabled.IsChecked == true); Refresh(); });
            idleMode.SelectionChanged += (s, e) => Changed();
            inGameMode.SelectionChanged += (s, e) => Changed();
            brightness.ValueChanged += (s, e) => Changed();
            alerts.ValueChanged += (s, e) => Changed();
            telemetrySpotter.Click += (s, e) => Changed(); telemetryYellow.Click += (s, e) => Changed();
            telemetryBlue.Click += (s, e) => Changed(); telemetryGreen.Click += (s, e) => Changed();
            telemetryWhite.Click += (s, e) => Changed();
            spotterColor.SelectionChanged += (s, e) => Changed();
            telemetryAbs.Click += (s, e) => Changed(); telemetryTc.Click += (s, e) => Changed();
            telemetryWheelLock.Click += (s, e) => Changed();
            warmth.ValueChanged += (s, e) => Changed();
            tint.ValueChanged += (s, e) => Changed();
            animationEffect.SelectionChanged += (s, e) => AnimationEffectChanged();
            animationSpeed.ValueChanged += (s, e) => Changed();
            animationIntensity.ValueChanged += (s, e) => Changed();
            randomPalette.Click += (s, e) => Changed();
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
        private static ComboBox ModeCombo(string label)
        {
            var panel = new ComboBox { Width = 390, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8), ToolTip = label };
            panel.Items.Add("Blanc fixe"); panel.Items.Add("Couleur fixe"); panel.Items.Add("Image des 3 écrans — SDR expérimental"); panel.Items.Add("Animations inspirées de WLED"); panel.Items.Add("RPM");
            return panel;
        }

        private void ApplyDarkStyles()
        {
            var background = new SolidColorBrush(Color.FromRgb(38, 38, 38));
            foreach (Type type in new[] { typeof(TabControl), typeof(TabItem), typeof(CheckBox), typeof(Button), typeof(ComboBox), typeof(ComboBoxItem), typeof(TextBox) })
            {
                var style = new Style(type, TryFindResource(type) as Style);
                style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
                style.Setters.Add(new Setter(Control.BackgroundProperty, background));
                if (type == typeof(ComboBox))
                {
                    // Some host/Windows templates paint white chrome regardless of Background.
                    // Keep selected values and editable port/profile text readable in dark mode.
                    var comboTemplate = (ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ComboBox}'>
  <Grid MinHeight='28' x:Name='Body'>
    <Border Background='{TemplateBinding Background}' BorderBrush='#666666' BorderThickness='1'/>
    <ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
      <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'>
        <Border Background='Transparent'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='White' StrokeThickness='1.5' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,8,0'/></Border>
      </ControlTemplate></ToggleButton.Template>
    </ToggleButton>
    <ContentPresenter x:Name='Selected' Margin='8,4,26,4' IsHitTestVisible='False'
                      Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'/>
    <TextBox x:Name='PART_EditableTextBox' Visibility='Hidden' Margin='5,2,25,2' Background='#262626' Foreground='White' BorderThickness='0'
             IsReadOnly='{TemplateBinding IsReadOnly}'/>
    <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False'
           IsOpen='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
      <Border Background='#262626' BorderBrush='#666666' BorderThickness='1' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'>
        <ScrollViewer MaxHeight='320' CanContentScroll='True'><ItemsPresenter/></ScrollViewer>
      </Border>
    </Popup>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsEditable' Value='True'>
      <Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/>
      <Setter TargetName='Selected' Property='Visibility' Value='Hidden'/>
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Body' Property='Opacity' Value='0.5'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");
                    style.Setters.Add(new Setter(Control.TemplateProperty, comboTemplate));
                    var editable = new Trigger { Property = ComboBox.IsEditableProperty, Value = true };
                    editable.Setters.Add(new Setter(Control.TemplateProperty, comboTemplate));
                    style.Triggers.Add(editable);
                }
                if (type == typeof(ComboBoxItem))
                {
                    var highlighted = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
                    highlighted.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.SteelBlue));
                    style.Triggers.Add(highlighted);
                }
                if (type == typeof(TabItem))
                {
                    style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(16, 8, 16, 8)));
                    var border = new FrameworkElementFactory(typeof(Border));
                    border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
                    border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
                    border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 3));
                    var label = new FrameworkElementFactory(typeof(ContentPresenter));
                    label.SetValue(ContentPresenter.ContentSourceProperty, "Header");
                    label.SetBinding(FrameworkElement.MarginProperty, new Binding("Padding") { RelativeSource = RelativeSource.TemplatedParent });
                    border.AppendChild(label);
                    style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(TabItem)) { VisualTree = border }));
                    var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
                    selected.Setters.Add(new Setter(Control.BackgroundProperty, background));
                    selected.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.SteelBlue));
                    style.Triggers.Add(selected);
                }
                Resources[type] = style;
            }
            Resources[SystemColors.WindowBrushKey] = background;
            Resources[SystemColors.WindowTextBrushKey] = Brushes.White;
            Resources[SystemColors.ControlBrushKey] = background;
            Resources[SystemColors.ControlTextBrushKey] = Brushes.White;
        }

        private void AddFirmwareControls(StackPanel root)
        {
            var content = root;
            content.Children.Add(new TextBlock { Text = "Firmware Adalight / FastLED", FontSize = 20 });
            content.Children.Add(Note("Base : ton firmware Adalight_WS2812 actuel, modifié pour l'extinction automatique. Dépôt GitHub privé : realisticsimcockpit/DIY-Ambient. GitHub CLI doit être connecté à un compte autorisé (gh auth login)."));
            content.Children.Add(Note("EVO : démarrage au noir, extinction après 1 seconde sans trame complète. 60 LED WS2812, DATA D6, 115200 bauds. Le choix 3 ou 5 bandes reste géré par le plugin."));
            content.Children.Add(Note("Carte indiquée : Nano. Vérifier ATmega328P et le bootloader ; les variantes WAVGAT ne sont pas toutes compatibles. Aucun flash automatique."));
            var board = new ComboBox { Width = 400, HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = -1 };
            foreach (string name in FirmwareFlasher.Boards) board.Items.Add(name);
            content.Children.Add(Note("Modèle / bootloader (choix obligatoire pour compiler ou flasher)"));
            content.Children.Add(board);
            var statusText = Note("Aucune opération firmware en cours.");
            FirmwareSource githubSource = null;
            var sourceText = Note("Source embarquée disponible pour compiler hors ligne. Pour flasher : charger d'abord le firmware depuis GitHub.");
            content.Children.Add(sourceText);
            var detected = Note("");
            content.Loaded += (s, e) => { if (plugin.Engine != null) detected.Text = plugin.Engine.FirmwareStatus; };
            content.Children.Add(detected);
            var confirmed = new CheckBox { Content = "J'ai vérifié la carte, le bootloader et DATA D6 ; autoriser le bouton de flash", Margin = new Thickness(0, 8, 0, 8), IsChecked = false };
            content.Children.Add(confirmed);
            var buttons = new WrapPanel(); content.Children.Add(buttons); content.Children.Add(statusText);
            Action<string> report = message => Dispatcher.BeginInvoke(new Action(() => statusText.Text = message));
            var prepare = new Button { Content = "Préparer les outils (Internet)", Margin = new Thickness(0, 4, 10, 4), Padding = new Thickness(12, 6, 12, 6) };
            var download = new Button { Content = "Charger depuis GitHub", Margin = prepare.Margin, Padding = prepare.Padding };
            var compile = new Button { Content = "Compiler sans flasher", Margin = prepare.Margin, Padding = prepare.Padding };
            var flash = new Button { Content = "Flasher la carte…", Margin = prepare.Margin, Padding = prepare.Padding, IsEnabled = false };
            buttons.Children.Add(prepare); buttons.Children.Add(download); buttons.Children.Add(compile); buttons.Children.Add(flash);
            confirmed.Click += (s, e) => flash.IsEnabled = confirmed.IsChecked == true && board.SelectedIndex >= 0 && githubSource != null;
            board.SelectionChanged += (s, e) => { confirmed.IsChecked = false; flash.IsEnabled = false; };
            download.Click += async (s, e) =>
            {
                IsEnabled = false; githubSource = null; confirmed.IsChecked = false; flash.IsEnabled = false;
                sourceText.Text = "Chargement du firmware GitHub…";
                try
                {
                    githubSource = await Task.Run(() => FirmwareFlasher.DownloadGitHub(report));
                    sourceText.Text = "Source GitHub : main / " + githubSource.Sha.Substring(0, 12) + " — 60 LED, D6, 115200 bauds";
                }
                catch (Exception ex) { sourceText.Text = "Téléchargement non effectué. Source embarquée : compilation seulement."; statusText.Text = ex.Message; }
                finally { IsEnabled = true; }
            };
            prepare.Click += async (s, e) =>
            {
                IsEnabled = false; statusText.Text = "Préparation des outils… Aucun accès au port COM.";
                try { await Task.Run(() => FirmwareFlasher.Prepare(report)); }
                catch (Exception ex) { statusText.Text = ex.Message; }
                finally { IsEnabled = true; }
            };
            compile.Click += async (s, e) =>
            {
                int target = board.SelectedIndex;
                IsEnabled = false; statusText.Text = "Compilation locale… Aucun accès au port COM.";
                var source = githubSource;
                try { await Task.Run(() => FirmwareFlasher.CompileOnly(target, report, source)); }
                catch (Exception ex) { statusText.Text = ex.Message; }
                finally { IsEnabled = true; }
            };
            flash.Click += async (s, e) =>
            {
                if (confirmed.IsChecked != true || board.SelectedIndex < 0 || githubSource == null) return;
                int target = board.SelectedIndex;
                var source = githubSource;
                string com = plugin.GetSettings().SerialPort;
                if (MessageBox.Show("Remplacer le firmware de " + FirmwareFlasher.Boards[target] + " sur " + com +
                    " ?\nSource GitHub : " + source.Sha.Substring(0, 12) + "\nLe firmware actuel n'est pas sauvegardé automatiquement.\n60 LED WS2812, DATA D6. Ne pas fermer SimHub ni débrancher la carte pendant l'opération.",
                    "Confirmer le flash", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                bool restore = plugin.Engine != null && plugin.Engine.State.Enabled;
                IsEnabled = false;
                try
                {
                    // Persist OFF so a game-change reinitialization cannot resume lighting mid-upload.
                    plugin.SetOutputEnabled(false);
                    await Task.Run(() => FirmwareFlasher.Flash(target, com, report, source));
                    if (restore && plugin.Engine != null) plugin.SetOutputEnabled(true);
                }
                catch (Exception ex) { statusText.Text = "Échec — éclairage laissé désactivé. " + ex.Message; }
                finally { IsEnabled = true; confirmed.IsChecked = false; flash.IsEnabled = false; Refresh(); }
            };
        }
        private static CheckBox TelemetryChoice(string text, Panel parent)
        {
            var choice = new CheckBox { Content = text, Margin = new Thickness(0, 2, 18, 5) };
            parent.Children.Add(choice); return choice;
        }
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

        private void TestTelemetry()
        {
            int index = Math.Max(0, telemetryTestEffect.SelectedIndex);
            plugin.Engine.TestTelemetry(TestEffects[index]);
        }

        private void LoadControls()
        {
            if (stopped || plugin.Engine == null) return;
            loading = true;
            Settings s = plugin.GetSettings();
            enabled.IsChecked = plugin.Engine.State.Enabled;
            idleMode.SelectedIndex = (int)s.IdleMode; inGameMode.SelectedIndex = (int)s.InGameMode; brightness.Value = s.Brightness * 100; alerts.Value = s.TelemetryLedCount;
            telemetrySpotter.IsChecked = s.TelemetrySpotterEnabled; telemetryYellow.IsChecked = s.TelemetryYellowEnabled;
            telemetryBlue.IsChecked = s.TelemetryBlueEnabled; telemetryGreen.IsChecked = s.TelemetryGreenEnabled;
            telemetryWhite.IsChecked = s.TelemetryWhiteEnabled;
            spotterColor.SelectedIndex = (int)s.SpotterColor;
            telemetryAbs.IsChecked = s.TelemetryAbsEnabled; telemetryTc.IsChecked = s.TelemetryTcEnabled;
            telemetryWheelLock.IsChecked = s.TelemetryWheelLockEnabled;
            warmth.Value = s.Warmth; tint.Value = s.Tint;
            animationEffect.SelectedIndex = Array.IndexOf(AnimationEffects, s.AnimationEffect);
            animationSpeed.Value = s.AnimationSpeed; animationIntensity.Value = s.AnimationIntensity;
            randomPalette.IsChecked = s.AnimationRandomPalette;
            keepOnExit.IsChecked = s.KeepOnAfterExit;
            stripCount.SelectedIndex = s.LedStripCount == 5 ? 1 : 0;
            UpdateModeControls(s);
            loading = false; Labels();
        }
        private void Labels()
        {
            brightnessLabel.Text = "Luminosité : " + ((int)brightness.Value) + " % du maximum configuré";
            int n = (int)alerts.Value;
            alertsLabel.Text = n == 0 ? "LED de télémétrie : désactivées" : string.Format("LED de télémétrie : {0} — {1} à gauche + {1} à droite", n, n / 2);
            animationSpeedLabel.Text = "Vitesse de l'animation : " + ((int)animationSpeed.Value);
            AnimationEffect effect = animationEffect.SelectedIndex >= 0 ? AnimationEffects[animationEffect.SelectedIndex] : AnimationEffect.Colorloop;
            bool adjustable = effect != AnimationEffect.Rainbow;
            randomPalette.Visibility = effect == AnimationEffect.Loading ? Visibility.Visible : Visibility.Collapsed;
            animationIntensityLabel.Visibility = adjustable ? Visibility.Visible : Visibility.Collapsed;
            animationIntensity.Visibility = adjustable ? Visibility.Visible : Visibility.Collapsed;
            animationIntensityLabel.Text = (effect == AnimationEffect.Colorloop ? "Saturation : " :
                effect == AnimationEffect.FireFlicker ? "Scintillement : " : "Fondu : ") + ((int)animationIntensity.Value);
        }
        private void UpdateModeControls(Settings s)
        {
            bool solid = s.IdleMode == LightingMode.Solid || s.InGameMode == LightingMode.Solid;
            bool animation = s.IdleMode == LightingMode.Animation || s.InGameMode == LightingMode.Animation;
            bool white = s.IdleMode == LightingMode.White || s.InGameMode == LightingMode.White;
            solidPalette.Visibility = solid || animation ? Visibility.Visible : Visibility.Collapsed;
            whiteControls.Visibility = white ? Visibility.Visible : Visibility.Collapsed;
            animationControls.Visibility = animation ? Visibility.Visible : Visibility.Collapsed;
        }
        private void Changed()
        {
            if (loading || stopped || idleMode.SelectedIndex < 0 || inGameMode.SelectedIndex < 0) return;
            Guard(() =>
            {
                Settings s = plugin.GetSettings(); s.IdleMode = (LightingMode)idleMode.SelectedIndex; s.InGameMode = (LightingMode)inGameMode.SelectedIndex; s.Mode = s.InGameMode;
                s.ModeProfilesInitialized = true;
                s.Brightness = brightness.Value / 100; s.TelemetryLedCount = (int)alerts.Value / 2 * 2;
                s.TelemetrySpotterEnabled = telemetrySpotter.IsChecked == true;
                s.TelemetryYellowEnabled = telemetryYellow.IsChecked == true;
                s.TelemetryBlueEnabled = telemetryBlue.IsChecked == true;
                s.TelemetryGreenEnabled = telemetryGreen.IsChecked == true;
                s.TelemetryWhiteEnabled = telemetryWhite.IsChecked == true;
                s.TelemetryAbsEnabled = telemetryAbs.IsChecked == true; s.TelemetryTcEnabled = telemetryTc.IsChecked == true;
                s.TelemetryWheelLockEnabled = telemetryWheelLock.IsChecked == true;
                s.TelemetryEffectsInitialized = true;
                s.SpotterColor = (SpotterColor)Math.Max(0, spotterColor.SelectedIndex);
                s.DrivingEffectsInitialized = true;
                s.Warmth = warmth.Value; s.Tint = tint.Value;
                AnimationEffect selectedEffect = AnimationEffects[Math.Max(0, animationEffect.SelectedIndex)];
                if (s.AnimationEffect != selectedEffect && selectedEffect == AnimationEffect.Loading)
                { s.SolidR = 255; s.SolidG = 160; s.SolidB = 0; }
                s.AnimationEffect = selectedEffect;
                s.AnimationSpeed = (int)animationSpeed.Value; s.AnimationIntensity = (int)animationIntensity.Value;
                s.AnimationRandomPalette = randomPalette.IsChecked == true; s.AnimationPaletteInitialized = true;
                s.RememberAnimation();
                s.KeepOnAfterExit = keepOnExit.IsChecked == true;
                s.LedStripCount = stripCount.SelectedIndex == 1 ? 5 : 3;
                plugin.ApplySettings(s, false); saveTimer.Stop(); saveTimer.Start(); Labels();
                UpdateModeControls(s);
            });
        }
        private void Refresh()
        {
            AmbientEngine current = plugin.Engine;
            if (current == null || stopped) return;
            // Actions/Stream Deck may change a mode while this page is open.
            // Keep the controls in sync without firing their change handlers.
            Settings canonical = plugin.GetSettings();
            if (idleMode.SelectedIndex != (int)canonical.IdleMode || inGameMode.SelectedIndex != (int)canonical.InGameMode || Math.Abs(brightness.Value - canonical.Brightness * 100) > .001 ||
                alerts.Value != canonical.TelemetryLedCount || Math.Abs(warmth.Value - canonical.Warmth) > .001 ||
                Math.Abs(tint.Value - canonical.Tint) > .001 || keepOnExit.IsChecked != canonical.KeepOnAfterExit ||
                telemetrySpotter.IsChecked != canonical.TelemetrySpotterEnabled || telemetryYellow.IsChecked != canonical.TelemetryYellowEnabled ||
                telemetryBlue.IsChecked != canonical.TelemetryBlueEnabled || telemetryGreen.IsChecked != canonical.TelemetryGreenEnabled ||
                telemetryWhite.IsChecked != canonical.TelemetryWhiteEnabled ||
                spotterColor.SelectedIndex != (int)canonical.SpotterColor ||
                telemetryAbs.IsChecked != canonical.TelemetryAbsEnabled || telemetryTc.IsChecked != canonical.TelemetryTcEnabled ||
                telemetryWheelLock.IsChecked != canonical.TelemetryWheelLockEnabled ||
                animationEffect.SelectedIndex != Array.IndexOf(AnimationEffects, canonical.AnimationEffect) || animationSpeed.Value != canonical.AnimationSpeed ||
                animationIntensity.Value != canonical.AnimationIntensity || randomPalette.IsChecked != canonical.AnimationRandomPalette ||
                stripCount.SelectedIndex != (canonical.LedStripCount == 5 ? 1 : 0))
                LoadControls();
            enabled.IsChecked = current.State.Enabled;
            activeGameLabel.Text = string.IsNullOrWhiteSpace(plugin.CurrentGameName) ? "Jeu actif : aucun" : "Jeu actif : " + plugin.CurrentGameName;
        }
        private Settings ReadInstallation()
        {
            Settings next = plugin.GetSettings();
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

        private void AnimationEffectChanged()
        {
            if (loading || stopped || animationEffect.SelectedIndex < 0) return;
            Guard(() => {
                Settings settings = plugin.GetSettings();
                settings.SelectAnimation(AnimationEffects[animationEffect.SelectedIndex]);
                plugin.ApplySettings(settings, true);
                LoadControls();
            });
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
