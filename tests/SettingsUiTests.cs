using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

class SettingsUiTests
{
    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject)
                foreach (var descendant in Descendants((DependencyObject)child)) yield return descendant;
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    [STAThread]
    static int Main(string[] args)
    {
        IDisposable engine = null;
        try
        {
            string host = Path.GetFullPath(args[1]);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) => {
                string file = Path.Combine(host, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var settingsType = assembly.GetType("DIYAmbient.Core.Settings", true);
            object settings = Activator.CreateInstance(settingsType);
            settingsType.GetField("SerialPort").SetValue(settings, ""); // No COM access.
            Type engineType = assembly.GetType("DIYAmbient.Plugin.AmbientEngine", true);
            engine = (IDisposable)Activator.CreateInstance(engineType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { settings }, null);
            Type pluginType = assembly.GetType("DIYAmbient.Plugin.AmbientPlugin", true);
            object plugin = Activator.CreateInstance(pluginType);
            pluginType.GetField("normalSettings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plugin, settings);
            pluginType.GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plugin, engine);
            Type uiType = assembly.GetType("DIYAmbient.Plugin.SettingsControl", true);
            var ui = (UserControl)Activator.CreateInstance(uiType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { plugin }, null);
            var tabs = Descendants(ui).OfType<TabControl>().Single();
            Assert(tabs.Items.Count == 3, "Expected exactly three tabs");
            string[] headers = { "Installation", "Personnalisation", "Firmware" };
            foreach (int index in new[] { 0, 1, 2 }) Assert((string)((TabItem)tabs.Items[index]).Header == headers[index], "Tab order");
            var installation = (DependencyObject)((TabItem)tabs.Items[0]).Content;
            foreach (string field in new[] { "port", "stripCount" })
                Assert(Descendants(installation).Contains((DependencyObject)uiType.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui)), "Misplaced " + field);
            Assert(Descendants(installation).OfType<Button>().Count(b => (b.Content as string) == "Configurer les zones") == 3, "Screen configuration missing");
            var firmware = (DependencyObject)((TabItem)tabs.Items[2]).Content;
            Assert(!Descendants(firmware).OfType<Button>().Single(b => (b.Content as string) == "Flasher la carte…").IsEnabled, "Flash must be locked initially");
            ui.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)); ui.Foreground = Brushes.White;
            Directory.CreateDirectory(args[2]);
            for (int index = 0; index < 3; index++)
            {
                tabs.SelectedIndex = index;
                ui.Measure(new Size(1040, 1400)); ui.Arrange(new Rect(0, 0, 1040, 1400)); ui.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1040, 1400, 96, 96, PixelFormats.Pbgra32); bitmap.Render(ui);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(args[2], "settings-" + index + ".png"))) encoder.Save(stream);
            }
            uiType.GetMethod("StopTimer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
            Console.WriteLine("PASS WPF construction with real SDK, three tabs, installation controls, three editors, flash locked. No settings changed; no port opened.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (engine != null) engine.Dispose(); }
    }
}
