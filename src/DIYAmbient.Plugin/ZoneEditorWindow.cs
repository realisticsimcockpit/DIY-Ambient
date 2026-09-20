using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal sealed class ZoneEditorWindow : Window
    {
        private readonly Canvas canvas = new Canvas();
        private readonly List<LedZone> zones;
        private readonly List<LedZone> original;
        private readonly Dictionary<int, Border> boxes = new Dictionary<int, Border>();
        private bool accepted;
        private readonly HashSet<int> selected = new HashSet<int>();
        private int selectionAnchor;
        private readonly TextBlock selectionLabel = new TextBlock { Foreground = Brushes.White };

        internal ZoneEditorWindow(Settings settings, MonitorInfo monitor)
        {
            Language = XmlLanguage.GetLanguage("fr-FR");
            zones = settings.Zones.Where(z => string.Equals(z.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (zones.Count == 0) throw new ArgumentException("Aucune zone affectée à cet écran.");
            original = zones.Select(z => z.Clone()).ToList(); selectionAnchor = zones[0].Led; selected.Add(selectionAnchor);
            Title = "DIY Ambient light EVO — zones " + monitor.DeviceName;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)); ShowInTaskbar = false; Topmost = true;
            Width = monitor.Bounds.Width; Height = monitor.Bounds.Height; Left = monitor.Bounds.Left; Top = monitor.Bounds.Top;
            var root = new Grid(); root.Children.Add(canvas); Content = root;
            var toolbar = new StackPanel { Margin = new Thickness(15) };
            var toolbarBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(28, 31, 38)),
                CornerRadius = new CornerRadius(8), Child = toolbar, Width = 520,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
            root.Children.Add(toolbarBorder);
            var handle = new Thumb { Height = 14, Cursor = Cursors.SizeAll, Background = Brushes.DimGray };
            toolbar.Children.Add(handle);
            var offset = new TranslateTransform(); toolbarBorder.RenderTransform = offset;
            handle.DragDelta += (s, e) => { offset.X += e.HorizontalChange; offset.Y += e.VerticalChange; e.Handled = true; };
            toolbar.Children.Add(new TextBlock { Text = monitor.ToString(), Foreground = Brushes.White, FontSize = 20 });
            toolbar.Children.Add(new TextBlock { Text = "Ctrl+clic : ajouter / retirer • Maj+clic : plage • Ctrl+A : tout sélectionner.\nTracer un rectangle dans le fond, puis glisser une bande sélectionnée pour déplacer le groupe. Coin inférieur droit : redimensionner une bande.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, Margin = new Thickness(0, 8, 0, 8) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(SettingsControl.Button("Enregistrer", () => { accepted = true; Close(); }));
            buttons.Children.Add(SettingsControl.Button("Annuler", () => Close()));
            buttons.Children.Add(SettingsControl.Button("Modèle triple écran", () => {
                DisplayMap display = settings.Displays.Single(d => string.Equals(d.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
                foreach (LedZone template in Settings.DefaultZones(display)) {
                    LedZone zone = zones.Single(z => z.Led == template.Led);
                    zone.X = template.X; zone.Y = template.Y; zone.Width = template.Width; zone.Height = template.Height;
                }
                selected.Clear(); foreach (var zone in zones) selected.Add(zone.Led); LayoutBoxes();
            })); toolbar.Children.Add(buttons);
            toolbar.Children.Add(selectionLabel);
            canvas.Background = Brushes.Transparent;
            ConfigureMarquee();
            foreach (LedZone zone in zones) CreateBox(zone);
            canvas.SizeChanged += (s, e) => LayoutBoxes(); Loaded += (s, e) => LayoutBoxes();
            SourceInitialized += (s, e) =>
            {
                using (new DpiScope()) Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), monitor.Bounds.Left,
                    monitor.Bounds.Top, monitor.Bounds.Width, monitor.Bounds.Height, 0x0040);
            };
            KeyDown += (s, e) => {
                if (e.Key == Key.Escape) Close();
                if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0) {
                    foreach (var zone in zones) selected.Add(zone.Led);
                    LayoutBoxes(); e.Handled = true;
                }
            };
            Closed += (s, e) =>
            {
                if (accepted) return;
                foreach (LedZone zone in zones)
                {
                    LedZone old = original.Single(o => o.Led == zone.Led);
                    zone.X = old.X; zone.Y = old.Y; zone.Width = old.Width; zone.Height = old.Height;
                }
            };
        }

        private void CreateBox(LedZone zone)
        {
            var content = new Grid();
            var box = new Border { BorderBrush = Brushes.Cyan, BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(48, 0, 160, 180)), Child = content, Cursor = Cursors.SizeAll };
            content.Children.Add(new TextBlock { Text = zone.Led.ToString(), FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var resize = new Thumb { Width = 15, Height = 15, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, Background = Brushes.White };
            content.Children.Add(resize);
            Point start = new Point(); bool dragging = false;
            List<LedZone> moving = null, origins = null;
            box.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (resize.IsMouseOver) return;
                bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                if (shift) {
                    if (!control) selected.Clear();
                    foreach (var item in zones.Where(z => z.Led >= Math.Min(selectionAnchor, zone.Led) && z.Led <= Math.Max(selectionAnchor, zone.Led))) selected.Add(item.Led);
                } else if (control) {
                    if (!selected.Add(zone.Led)) selected.Remove(zone.Led);
                    selectionAnchor = zone.Led;
                } else {
                    if (!selected.Contains(zone.Led)) { selected.Clear(); selected.Add(zone.Led); }
                    selectionAnchor = zone.Led;
                }
                LayoutBoxes(); e.Handled = true;
                if (!selected.Contains(zone.Led)) return;
                start = e.GetPosition(canvas);
                moving = zones.Where(z => selected.Contains(z.Led)).ToList(); origins = moving.Select(z => z.Clone()).ToList();
                dragging = box.CaptureMouse(); LayoutBoxes(); e.Handled = true;
            };
            box.MouseMove += (s, e) =>
            {
                if (!dragging || canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;
                Point p = e.GetPosition(canvas);
                ZoneLayout.MoveGroup(moving, origins, (p.X - start.X) / canvas.ActualWidth, (p.Y - start.Y) / canvas.ActualHeight);
                LayoutBoxes();
            };
            box.MouseLeftButtonUp += (s, e) => { dragging = false; box.ReleaseMouseCapture(); };
            box.LostMouseCapture += (s, e) => { dragging = false; };
            resize.DragDelta += (s, e) =>
            {
                if (canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;
                selected.Clear(); selected.Add(zone.Led); selectionAnchor = zone.Led;
                zone.Width = Clamp(zone.Width + e.HorizontalChange / canvas.ActualWidth, .005, 1 - zone.X);
                zone.Height = Clamp(zone.Height + e.VerticalChange / canvas.ActualHeight, .005, 1 - zone.Y);
                LayoutBoxes(); e.Handled = true;
            };
            boxes[zone.Led] = box; canvas.Children.Add(box);
        }

        private void LayoutBoxes()
        {
            foreach (LedZone zone in zones)
            {
                Border box = boxes[zone.Led]; Canvas.SetLeft(box, zone.X * canvas.ActualWidth); Canvas.SetTop(box, zone.Y * canvas.ActualHeight);
                box.Width = Math.Max(1, zone.Width * canvas.ActualWidth); box.Height = Math.Max(1, zone.Height * canvas.ActualHeight);
                box.BorderBrush = selected.Contains(zone.Led) ? Brushes.Yellow : Brushes.Cyan;
            }
            selectionLabel.Text = selected.Count + " bande(s) sélectionnée(s)";
        }

        private void ConfigureMarquee()
        {
            var marquee = new Border { BorderBrush = Brushes.Yellow, BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(32, 255, 220, 0)), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            Canvas.SetZIndex(marquee, 10); canvas.Children.Add(marquee);
            Point start = new Point(); bool active = false; HashSet<int> baseline = null;
            canvas.MouseLeftButtonDown += (s, e) => {
                if (e.OriginalSource != canvas) return;
                baseline = (Keyboard.Modifiers & ModifierKeys.Control) != 0 ? new HashSet<int>(selected) : new HashSet<int>();
                selected.Clear(); selected.UnionWith(baseline);
                start = e.GetPosition(canvas); active = canvas.CaptureMouse();
                Canvas.SetLeft(marquee, start.X); Canvas.SetTop(marquee, start.Y); marquee.Width = marquee.Height = 0;
                marquee.Visibility = Visibility.Visible; LayoutBoxes(); e.Handled = true;
            };
            canvas.MouseMove += (s, e) => {
                if (!active || canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;
                var bounds = new Rect(start, e.GetPosition(canvas));
                Canvas.SetLeft(marquee, bounds.Left); Canvas.SetTop(marquee, bounds.Top);
                marquee.Width = bounds.Width; marquee.Height = bounds.Height;
                selected.Clear(); selected.UnionWith(baseline);
                foreach (var zone in zones) if (bounds.IntersectsWith(new Rect(zone.X * canvas.ActualWidth, zone.Y * canvas.ActualHeight,
                    zone.Width * canvas.ActualWidth, zone.Height * canvas.ActualHeight))) selected.Add(zone.Led);
                LayoutBoxes();
            };
            canvas.MouseLeftButtonUp += (s, e) => { if (active) { active = false; marquee.Visibility = Visibility.Collapsed; canvas.ReleaseMouseCapture(); e.Handled = true; } };
            canvas.LostMouseCapture += (s, e) => { if (e.OriginalSource == canvas) { active = false; marquee.Visibility = Visibility.Collapsed; } };
        }
        private static double Clamp(double value, double minimum, double maximum) { return Math.Min(maximum, Math.Max(minimum, value)); }
    }
}
