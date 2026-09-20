using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal sealed class ZoneEditorWindow : Window
    {
        private readonly Canvas canvas = new Canvas();
        private readonly List<LedZone> zones;
        private readonly Dictionary<int, Border> boxes = new Dictionary<int, Border>();
        private readonly List<LedZone> original;
        private readonly TextBlock selectionLabel;
        private bool accepted;
        private int selected;

        internal ZoneEditorWindow(Settings settings, MonitorInfo monitor, AmbientEngine engine)
        {
            zones = settings.Zones.Where(z => string.Equals(z.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (zones.Count == 0) throw new ArgumentException("Aucune zone affectée à cet écran.");
            original = zones.Select(z => z.Clone()).ToList(); selected = zones[0].Led;
            Title = "DIY-Ambient — zones " + monitor.DeviceName;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0));
            ShowInTaskbar = false; Topmost = true;
            Width = monitor.Bounds.Width; Height = monitor.Bounds.Height;
            Left = monitor.Bounds.Left; Top = monitor.Bounds.Top;
            var root = new Grid(); root.Children.Add(canvas); Content = root;
            var toolbar = new StackPanel { Margin = new Thickness(15) };
            var toolbarBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(28, 31, 38)),
                CornerRadius = new CornerRadius(8), Child = toolbar, Width = 560,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            root.Children.Add(toolbarBorder);
            var paletteOffset = new TranslateTransform(); toolbarBorder.RenderTransform = paletteOffset;
            var paletteHandle = new Thumb { Height = 14, Cursor = Cursors.SizeAll, Background = Brushes.DimGray,
                ToolTip = "Déplacer cette palette pour atteindre les zones situées dessous" };
            toolbar.Children.Add(paletteHandle);
            paletteHandle.DragDelta += (s, e) =>
            {
                Point origin = toolbarBorder.TranslatePoint(new Point(0, 0), root);
                paletteOffset.X += Clamp(e.HorizontalChange, -origin.X, Math.Max(-origin.X, root.ActualWidth - origin.X - toolbarBorder.ActualWidth));
                paletteOffset.Y += Clamp(e.VerticalChange, -origin.Y, Math.Max(-origin.Y, root.ActualHeight - origin.Y - toolbarBorder.ActualHeight));
                e.Handled = true;
            };
            toolbar.Children.Add(new TextBlock { Text = monitor.ToString(), Foreground = Brushes.White, FontSize = 20 });
            toolbar.Children.Add(new TextBlock { Text = "Déplacer les rectangles. Redimensionner par le coin inférieur droit.\nLes numéros sont les adresses physiques du ruban.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, Margin = new Thickness(0, 8, 0, 8) });
            selectionLabel = new TextBlock { Foreground = Brushes.White, Text = "LED sélectionnée : " + selected }; toolbar.Children.Add(selectionLabel);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(SettingsControl.Button("Identifier · 3 s", () => engine.Test(false, false, selected)));
            buttons.Children.Add(SettingsControl.Button("Valider", () => { accepted = true; Close(); }));
            buttons.Children.Add(SettingsControl.Button("Annuler", () => Close())); toolbar.Children.Add(buttons);
            toolbar.Children.Add(new TextBlock { Text = "Identification réelle seulement si éclairage activé et matériel autorisé.\nÉchap annule. Le fond lumineux est suspendu pendant le placement.", Foreground = Brushes.LightGray, FontSize = 11 });

            foreach (LedZone zone in zones) CreateBox(zone);
            canvas.SizeChanged += (s, e) => LayoutBoxes();
            SourceInitialized += (s, e) =>
            {
                using (new DpiScope())
                    if (!Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), monitor.Bounds.Left,
                        monitor.Bounds.Top, monitor.Bounds.Width, monitor.Bounds.Height, 0x0040))
                        Storage.Log("Zone editor placement failed: " + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            };
            Loaded += (s, e) => LayoutBoxes();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Closed += (s, e) =>
            {
                engine.ClearTest();
                if (accepted) return;
                foreach (LedZone z in zones)
                {
                    LedZone old = original.Single(o => o.Led == z.Led);
                    z.X = old.X; z.Y = old.Y; z.Width = old.Width; z.Height = old.Height;
                }
            };
        }

        private void CreateBox(LedZone zone)
        {
            var contents = new Grid();
            var box = new Border { BorderBrush = Brushes.Cyan, BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(48, 0, 160, 180)), Child = contents, Cursor = Cursors.SizeAll };
            contents.Children.Add(new TextBlock { Text = zone.Led.ToString(), FontSize = 22, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var handle = new Thumb { Width = 15, Height = 15, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, Background = Brushes.White };
            contents.Children.Add(handle);
            Point start = new Point(); double originalX = 0, originalY = 0; bool dragging = false;
            box.MouseLeftButtonDown += (s, e) =>
            {
                if (InsideThumb(e.OriginalSource as DependencyObject)) return;
                selected = zone.Led; selectionLabel.Text = "LED sélectionnée : " + selected;
                start = e.GetPosition(canvas); originalX = zone.X; originalY = zone.Y;
                dragging = box.CaptureMouse(); LayoutBoxes(); e.Handled = true;
            };
            box.MouseMove += (s, e) =>
            {
                if (!dragging || canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;
                Point p = e.GetPosition(canvas);
                zone.X = Clamp(originalX + (p.X - start.X) / canvas.ActualWidth, 0, 1 - zone.Width);
                zone.Y = Clamp(originalY + (p.Y - start.Y) / canvas.ActualHeight, 0, 1 - zone.Height);
                LayoutBoxes();
            };
            box.MouseLeftButtonUp += (s, e) => { dragging = false; box.ReleaseMouseCapture(); };
            box.LostMouseCapture += (s, e) => dragging = false;
            handle.DragStarted += (s, e) =>
            {
                selected = zone.Led; selectionLabel.Text = "LED sélectionnée : " + selected;
                LayoutBoxes(); e.Handled = true;
            };
            handle.DragDelta += (s, e) =>
            {
                if (canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;
                selected = zone.Led; selectionLabel.Text = "LED sélectionnée : " + selected;
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
                Border box = boxes[zone.Led];
                Canvas.SetLeft(box, zone.X * canvas.ActualWidth); Canvas.SetTop(box, zone.Y * canvas.ActualHeight);
                box.Width = Math.Max(1, zone.Width * canvas.ActualWidth); box.Height = Math.Max(1, zone.Height * canvas.ActualHeight);
                box.BorderBrush = selected == zone.Led ? Brushes.Yellow : Brushes.Cyan;
            }
        }
        private static bool InsideThumb(DependencyObject node)
        {
            while (node != null)
            {
                if (node is Thumb) return true;
                if (!(node is Visual)) return false;
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }
        private static double Clamp(double x, double lo, double hi) { return Math.Min(hi, Math.Max(lo, x)); }
    }
}
