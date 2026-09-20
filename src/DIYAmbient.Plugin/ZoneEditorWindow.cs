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
        private int selected;

        internal ZoneEditorWindow(Settings settings, MonitorInfo monitor)
        {
            Language = XmlLanguage.GetLanguage("fr-FR");
            zones = settings.Zones.Where(z => string.Equals(z.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (zones.Count == 0) throw new ArgumentException("Aucune zone affectée à cet écran.");
            original = zones.Select(z => z.Clone()).ToList(); selected = zones[0].Led;
            Title = "DIY Ambient light EVO — zones " + monitor.DeviceName;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)); ShowInTaskbar = false; Topmost = true;
            Width = monitor.Bounds.Width; Height = monitor.Bounds.Height; Left = monitor.Bounds.Left; Top = monitor.Bounds.Top;
            var root = new Grid(); root.Children.Add(canvas); Content = root;
            var toolbar = new StackPanel { Margin = new Thickness(15) };
            var toolbarBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(28, 31, 38)),
                CornerRadius = new CornerRadius(8), Child = toolbar, Width = 520,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            root.Children.Add(toolbarBorder);
            var handle = new Thumb { Height = 14, Cursor = Cursors.SizeAll, Background = Brushes.DimGray };
            toolbar.Children.Add(handle);
            var offset = new TranslateTransform(); toolbarBorder.RenderTransform = offset;
            handle.DragDelta += (s, e) => { offset.X += e.HorizontalChange; offset.Y += e.VerticalChange; e.Handled = true; };
            toolbar.Children.Add(new TextBlock { Text = monitor.ToString(), Foreground = Brushes.White, FontSize = 20 });
            toolbar.Children.Add(new TextBlock { Text = "Déplacer les blocs LED et les redimensionner par le coin inférieur droit.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, Margin = new Thickness(0, 8, 0, 8) });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(SettingsControl.Button("Enregistrer", () => { accepted = true; Close(); }));
            buttons.Children.Add(SettingsControl.Button("Annuler", () => Close())); toolbar.Children.Add(buttons);
            foreach (LedZone zone in zones) CreateBox(zone);
            canvas.SizeChanged += (s, e) => LayoutBoxes(); Loaded += (s, e) => LayoutBoxes();
            SourceInitialized += (s, e) =>
            {
                using (new DpiScope()) Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), monitor.Bounds.Left,
                    monitor.Bounds.Top, monitor.Bounds.Width, monitor.Bounds.Height, 0x0040);
            };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
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
            Point start = new Point(); double startX = 0, startY = 0; bool dragging = false;
            box.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Thumb) return;
                selected = zone.Led; start = e.GetPosition(canvas); startX = zone.X; startY = zone.Y;
                dragging = box.CaptureMouse(); LayoutBoxes(); e.Handled = true;
            };
            box.MouseMove += (s, e) =>
            {
                if (!dragging || canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;
                Point p = e.GetPosition(canvas);
                zone.X = Clamp(startX + (p.X - start.X) / canvas.ActualWidth, 0, 1 - zone.Width);
                zone.Y = Clamp(startY + (p.Y - start.Y) / canvas.ActualHeight, 0, 1 - zone.Height); LayoutBoxes();
            };
            box.MouseLeftButtonUp += (s, e) => { dragging = false; box.ReleaseMouseCapture(); };
            resize.DragDelta += (s, e) =>
            {
                selected = zone.Led;
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
                box.BorderBrush = zone.Led == selected ? Brushes.Yellow : Brushes.Cyan;
            }
        }
        private static double Clamp(double value, double minimum, double maximum) { return Math.Min(maximum, Math.Max(minimum, value)); }
    }
}
