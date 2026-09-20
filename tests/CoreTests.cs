using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using DIYAmbient.Core;

internal static class CoreTests
{
    private static int count, failed;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Equal(int a, int b, string name) { Check(a == b, name + ": " + a + " != " + b); }
    private static void Test(string name, Action body)
    {
        count++;
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Reject(Action body)
    {
        bool rejected = false;
        try { body(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid configuration was accepted");
    }
    private static Settings Full()
    { var s = new Settings(); s.Brightness = 1; s.CurrentBudgetAmps = 5; return s; }
    private static Rgb[] Fill(Rgb color) { return Enumerable.Repeat(color, 60).ToArray(); }
    private static FrameResult Compose(Settings s, bool enabled, TelemetrySnapshot t)
    { return FrameComposer.Compose(s, enabled, null, t, new TelemetrySelection(s), DateTime.UtcNow, 0); }

    private enum UnknownFlag { NamedTwo = 2 }
    private sealed class Flags
    {
        public bool Flag = true;
        public int Number = 1;
        public string Text = "1";
        public UnknownFlag Enumeration = (UnknownFlag)1;
        public bool? Optional;
        public bool Broken { get { throw new InvalidOperationException("getter failure"); } }
        public int this[int index] { get { return index; } }
    }

    public static int Main()
    {
        try
        {
            Test("Default settings validate and remain preview-only", () => {
                var s = new Settings(); s.Validate(); Check(s.PreviewOnly && !s.ElectricalConfirmed, "Unsafe defaults");
                Equal(s.TelemetryLedCount, 0, "Default telemetry");
            });
            Test("Existing display ranges", () => {
                var s = new Settings();
                Equal(s.Displays.Single(d => d.Position == 0).FirstLed, 21, "Center start");
                Equal(s.Displays.Single(d => d.Position == 0).LastLed, 40, "Center end");
                Equal(s.Zones.Count, 60, "Zone count");
            });
            Test("All even telemetry counts have equal disjoint sides", () => {
                var s = new Settings();
                for (int n = 0; n <= 60; n += 2) {
                    s.TelemetryLedCount = n; var selection = new TelemetrySelection(s);
                    Equal(selection.Left.Length, n / 2, "Left"); Equal(selection.Right.Length, n / 2, "Right");
                    Equal(selection.Left.Intersect(selection.Right).Count(), 0, "No overlap");
                }
            });
            Test("Odd telemetry counts rejected", () => { var s = new Settings(); s.TelemetryLedCount = 11; Reject(s.Validate); });
            Test("Out-of-range telemetry rejected", () => { var s = new Settings(); s.TelemetryLedCount = 62; Reject(s.Validate); });
            Test("Bad display overlap rejected", () => { var s = new Settings(); s.Displays[0].LastLed = 21; Reject(s.Validate); });
            Test("Duplicate LED rejected", () => { var s = new Settings(); s.Zones[1].Led = 1; Reject(s.Validate); });
            Test("Out-of-screen rectangle rejected", () => { var s = new Settings(); s.Zones[0].X = .99; Reject(s.Validate); });
            Test("NaN brightness rejected", () => { var s = new Settings(); s.Brightness = double.NaN; Reject(s.Validate); });
            Test("Unknown schema rejected", () => { var s = new Settings(); s.SchemaVersion = 2; Reject(s.Validate); });
            Test("Clone is independent", () => { var a = new Settings(); var b = a.Clone(); b.Zones[0].X = .5; Check(a.Zones[0].X == 0, "Aliased zones"); });
            Test("Configuration JSON roundtrip", () => {
                var s = new Settings(); var serializer = new DataContractJsonSerializer(typeof(Settings));
                using (var stream = new MemoryStream()) {
                    serializer.WriteObject(stream, s); stream.Position = 0;
                    var result = (Settings)serializer.ReadObject(stream); result.Validate(); Equal(result.Zones.Count, 60, "Roundtrip");
                }
            });
            Test("Adalight header / RGB order / 186 bytes", () => {
                var colors = Fill(Rgb.Black); colors[0] = new Rgb(1, 2, 3); colors[59] = new Rgb(250, 251, 252);
                byte[] p = AdalightProtocol.Encode(colors);
                Equal(p.Length, 186, "Length");
                Check(p.Take(6).SequenceEqual(new byte[] { 65, 100, 97, 0, 59, 110 }), "Header");
                Check(p.Skip(6).Take(3).SequenceEqual(new byte[] { 1, 2, 3 }), "RGB order");
                Check(p.Skip(183).SequenceEqual(new byte[] { 250, 251, 252 }), "Final LED");
            });
            Test("Never send a shortened frame", () => Reject(() => AdalightProtocol.Encode(new Rgb[40])));
            Test("Off remains black even with alerts", () => {
                var s = Full(); s.TelemetryLedCount = 60;
                var t = new TelemetrySnapshot(true, true, true, true, true, DateTime.UtcNow);
                Check(Compose(s, false, t).Colors.All(c => c.R + c.G + c.B == 0), "Off bypassed");
            });
            Test("White is uniform RGB before physical calibration", () => {
                var s = Full(); var f = Compose(s, true, TelemetrySnapshot.Empty);
                Check(f.Colors.All(c => c.R == 255 && c.G == 255 && c.B == 255), "Uneven white");
            });
            Test("Spotter temporarily replaces exactly five LEDs", () => {
                var s = Full(); s.TelemetryLedCount = 10;
                var t = new TelemetrySnapshot(true, true, false, false, false, DateTime.UtcNow);
                var f = Compose(s, true, t);
                Equal(f.Colors.Count(c => c.R == 255 && c.G == 0 && c.B == 0), 5, "Left LEDs");
                Equal(Compose(s, true, TelemetrySnapshot.Empty).Colors.Count(c => c.G == 255), 60, "Restored background");
            });
            Test("Expired and future telemetry are suppressed", () => {
                var s = Full(); s.TelemetryLedCount = 60;
                foreach (int offset in new[] { -10, 10 }) {
                    var t = new TelemetrySnapshot(true, true, true, true, false, DateTime.UtcNow.AddSeconds(offset));
                    Check(Compose(s, true, t).Colors.All(c => c.G == 255), "Stale/future alert");
                }
            });
            Test("Power budget applies to white and alerts", () => {
                var s = Full(); s.CurrentBudgetAmps = .5; s.TelemetryLedCount = 60;
                var t = new TelemetrySnapshot(true, true, true, true, false, DateTime.UtcNow);
                Check(Compose(s, true, t).EstimatedAmps <= .50000001, "Alert exceeds estimate");
                Check(Compose(s, true, TelemetrySnapshot.Empty).EstimatedAmps <= .50000001, "White exceeds estimate");
            });
            Test("Brightness remains effective below a limited maximum", () => {
                var s = Full(); s.CurrentBudgetAmps = .5;
                var max = Compose(s, true, TelemetrySnapshot.Empty); s.Brightness = .5;
                var half = Compose(s, true, TelemetrySnapshot.Empty);
                Check(half.EstimatedAmps < max.EstimatedAmps * .65, "Slider plateau");
            });
            Test("Random frame current estimates never exceed budget", () => {
                var rng = new Random(403); var s = Full();
                for (int round = 0; round < 1000; round++) {
                    s.CurrentBudgetAmps = .06 + rng.NextDouble() * 2; s.Brightness = rng.NextDouble();
                    s.Warmth = rng.NextDouble() * 2 - 1; s.Tint = rng.NextDouble() * 2 - 1;
                    var input = new Rgb[60];
                    for (int i = 0; i < 60; i++) input[i] = new Rgb((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256));
                    Check(FrameComposer.ApplyOutputLimits(s, input).EstimatedAmps <= s.CurrentBudgetAmps + 1e-8, "Budget exceeded");
                }
            });
            Test("BGRA zones sample only their local rectangle", () => {
                byte[] data = { 0, 0, 255, 255, 255, 0, 0, 255 }; // red then blue
                var zone = new LedZone { X = .5, Y = 0, Width = .5, Height = 1 };
                var c = ZoneSampler.Average(data, 2, 1, 8, zone);
                Equal(c.R, 0, "R"); Equal(c.B, 255, "B");
            });
            Test("Regression: spotter never brightens unrelated white LEDs", () => {
                var s = Full(); s.CurrentBudgetAmps = .5; s.TelemetryLedCount = 10;
                var before = Compose(s, true, TelemetrySnapshot.Empty);
                var during = Compose(s, true, new TelemetrySnapshot(true, true, false, false, false, DateTime.UtcNow));
                var left = new TelemetrySelection(s).Left;
                for (int i = 0; i < 60; i++) if (!left.Contains(i + 1))
                    Check(before.Colors[i].ToString() == during.Colors[i].ToString(), "Unrelated LED changed: " + (i + 1));
            });
            Test("Fixed ceiling independent of image content", () => {
                var s = Full(); s.CurrentBudgetAmps = .5;
                var a = Fill(Rgb.White); var b = Fill(Rgb.Black); b[0] = Rgb.White;
                Check(FrameComposer.ApplyOutputLimits(s, a).Colors[0].ToString() ==
                    FrameComposer.ApplyOutputLimits(s, b).Colors[0].ToString(), "Image-dependent gain");
            });
            Test("White adjustment is effective in white mode", () => {
                var s = Full(); s.Warmth = 1; var c = Compose(s, true, TelemetrySnapshot.Empty).Colors[0];
                Check(c.R > c.B, "Warm white not adjusted");
            });
            Test("White adjustment leaves solid RGB untouched", () => {
                var s = Full(); s.Mode = LightingMode.Solid; s.SolidR = 13; s.SolidG = 77; s.SolidB = 201;
                var a = Compose(s, true, TelemetrySnapshot.Empty); s.Warmth = 1; s.Tint = -1;
                Check(a.Colors[0].ToString() == Compose(s, true, TelemetrySnapshot.Empty).Colors[0].ToString(), "Solid color tinted");
            });
            Test("White adjustment leaves screen pixels untouched", () => {
                var s = Full(); s.Mode = LightingMode.Screen; s.Warmth = 1; s.Tint = -1;
                var input = Fill(new Rgb(23, 99, 177));
                var f = FrameComposer.Compose(s, true, input, TelemetrySnapshot.Empty, new TelemetrySelection(s), DateTime.UtcNow, 0);
                Check(f.Colors[0].ToString() == input[0].ToString(), "Screen tinted by white preset");
            });
            Test("White adjustment leaves blue flag untouched", () => {
                var s = Full(); s.Warmth = 1; s.Tint = -1; s.TelemetryLedCount = 60;
                var f = Compose(s, true, new TelemetrySnapshot(true, false, false, false, true, DateTime.UtcNow));
                Check(f.Colors.All(c => c.R == 0 && c.G == 70 && c.B == 255), "Blue flag tinted");
            });
            Test("Identification uses the common current ceiling", () => {
                var s = Full(); s.CurrentBudgetAmps = .2;
                var f = FrameComposer.Compose(s, true, null, TelemetrySnapshot.Empty, new TelemetrySelection(s), DateTime.UtcNow, 60);
                Equal(f.Colors.Take(59).Sum(c => c.R + c.G + c.B), 0, "Other LEDs not black");
                Check(f.EstimatedAmps <= .2, "Identification exceeded ceiling");
            });
            Test("Zero brightness suppresses all alerts", () => {
                var s = Full(); s.Brightness = 0; s.TelemetryLedCount = 60;
                Check(Compose(s, true, new TelemetrySnapshot(true, true, true, true, true, DateTime.UtcNow)).Colors.All(c => c.R + c.G + c.B == 0), "Brightness bypassed");
            });
            Test("Zero telemetry count preserves the entire background", () => {
                var s = Full(); s.TelemetryLedCount = 0;
                Check(Compose(s, true, new TelemetrySnapshot(true, true, true, true, true, DateTime.UtcNow)).Colors.All(c => c.G == 255), "Disabled telemetry active");
            });
            Test("Two simultaneous sides replace all 60 at maximum count", () => {
                var s = Full(); s.TelemetryLedCount = 60;
                Check(Compose(s, true, new TelemetrySnapshot(true, true, true, false, false, DateTime.UtcNow)).Colors.All(c => c.R == 255 && c.G == 0 && c.B == 0), "Missing side LEDs");
            });
            Test("Right-only spotter does not paint left-side selection", () => {
                var s = Full(); s.TelemetryLedCount = 10;
                var f = Compose(s, true, new TelemetrySnapshot(true, false, true, false, false, DateTime.UtcNow));
                var groups = new TelemetrySelection(s);
                Check(groups.Right.All(i => f.Colors[i - 1].G == 0), "Right missing");
                Check(groups.Left.All(i => f.Colors[i - 1].G == 255), "Left wrongly painted");
            });
            Test("Telemetry routing is invariant to zone list ordering", () => {
                var s = Full(); s.TelemetryLedCount = 20; var before = new TelemetrySelection(s);
                s.Zones.Reverse(); s.Displays.Reverse(); var after = new TelemetrySelection(s);
                Check(before.Left.SequenceEqual(after.Left) && before.Right.SequenceEqual(after.Right), "Unstable routing");
            });
            Test("Left roles override Windows display number", () => {
                var s = Full(); s.TelemetryLedCount = 10;
                Check(new TelemetrySelection(s).Left.All(i => i <= 20), "DISPLAY1 wrongly treated as left");
                Check(new TelemetrySelection(s).Right.All(i => i >= 41), "Right role ignored");
            });
            Test("Hardware changes are detected independently of mode", () => {
                var a = Full(); var b = a.Clone();
                b.Mode = LightingMode.Screen; Check(!OutputPolicy.HardwareChanged(a, b), "Mode treated as wiring change");
                b.CurrentBudgetAmps = .5; Check(OutputPolicy.HardwareChanged(a, b), "Budget not disarmed");
                b = a.Clone(); b.SerialPort = "COM3"; Check(OutputPolicy.HardwareChanged(a, b), "Port not disarmed");
                b = a.Clone(); b.PreviewOnly = false; Check(OutputPolicy.HardwareChanged(a, b), "Preview transition not disarmed");
                b = a.Clone(); b.ElectricalConfirmed = true; Check(OutputPolicy.HardwareChanged(a, b), "Confirmation change ignored");
            });
            Test("Port comparison is case-insensitive", () => {
                var a = Full(); a.SerialPort = "COM3"; var b = a.Clone(); b.SerialPort = "com3";
                b.Validate(); Check(!OutputPolicy.HardwareChanged(a, b), "Case-only change");
            });
            Test("Malformed and non-COM port names rejected", () => {
                foreach (string value in new[] { "COM0", "COM03", "COM3\n", "COM3:bad", "C:/file", "socket:host", "COM" }) {
                    var s = Full(); s.SerialPort = value; Reject(s.Validate);
                }
            });
            Test("Layout comparison ignores color and brightness changes", () => {
                var a = Full(); var b = a.Clone(); b.Brightness = .1; b.Warmth = 1; b.TelemetryLedCount = 60;
                Check(OutputPolicy.CaptureLayoutEquals(a, b), "Unneeded capture invalidation");
            });
            Test("Layout changes invalidate old capture", () => {
                var a = Full(); var b = a.Clone(); b.Zones[0].X += .01;
                Check(!OutputPolicy.CaptureLayoutEquals(a, b), "Moved zone not detected");
                Check(!OutputPolicy.CanUseCapture(2, 1, .1), "Old layout reused");
            });
            Test("Capture freshness rejects missing/future/stale/NaN data", () => {
                Check(OutputPolicy.CanUseCapture(2, 2, .1), "Fresh rejected");
                foreach (double age in new[] { -.1, .75, 2.0, double.NaN, double.PositiveInfinity })
                    Check(!OutputPolicy.CanUseCapture(2, 2, age), "Invalid age accepted");
            });
            Test("Zone sampler rejects null zones", () => Reject(() => ZoneSampler.Average(new byte[4], 1, 1, 4, null)));
            Test("Zone sampler rejects overflow dimensions", () => Reject(() => ZoneSampler.Average(new byte[4], int.MaxValue, 2, 4, new LedZone())));
            Test("Zone sampler rejects zero-area and NaN rectangles", () => {
                var zone = new LedZone { X = 0, Y = 0, Width = 0, Height = 1 };
                Reject(() => ZoneSampler.Average(new byte[4], 1, 1, 4, zone));
                zone.Width = 1; zone.X = double.NaN; Reject(() => ZoneSampler.Average(new byte[4], 1, 1, 4, zone));
            });
            Test("Zone sampler honors row padding", () => {
                byte[] pixels = { 0, 0, 255, 0, 99, 99, 99, 99, 255, 0, 0, 0, 99, 99, 99, 99 };
                var c = ZoneSampler.Average(pixels, 1, 2, 8, new LedZone { Width = 1, Height = 1 });
                Check(c.R == 127 && c.B == 127 && c.G == 0, "Padding sampled");
            });
            Test("All 60 physical RGB slots preserve their order", () => {
                var colors = new Rgb[60];
                for (int i = 0; i < 60; i++) colors[i] = new Rgb((byte)i, (byte)(i + 60), (byte)(i + 120));
                byte[] p = AdalightProtocol.Encode(colors);
                for (int i = 0; i < 60; i++) {
                    Equal(p[6 + i * 3], i, "R slot"); Equal(p[7 + i * 3], i + 60, "G slot"); Equal(p[8 + i * 3], i + 120, "B slot");
                }
            });
            Test("Null and oversized Adalight frames rejected", () => { Reject(() => AdalightProtocol.Encode(null)); Reject(() => AdalightProtocol.Encode(new Rgb[61])); });
            Test("Boolean telemetry fields are recognized", () => {
                var reader = new TelemetryFlagReader(); bool available;
                Check(reader.Read(new Flags(), "Flag", out available) && available, "Boolean field unreadable");
            });
            Test("Missing and nullable-null telemetry stay unavailable", () => {
                var reader = new TelemetryFlagReader(); bool available;
                Check(!reader.Read(new Flags(), "Missing", out available) && !available, "Missing accepted");
                Check(!reader.Read(new Flags(), "Optional", out available) && !available, "Null accepted");
            });
            Test("Only binary numeric telemetry is recognized", () => {
                var reader = new TelemetryFlagReader(); bool available; var f = new Flags();
                Check(reader.Read(f, "Number", out available) && available, "One not recognized");
                f.Number = 0; Check(!reader.Read(f, "Number", out available) && available, "Zero not recognized");
                f.Number = 2; Check(!reader.Read(f, "Number", out available) && !available, "Multistate guessed");
            });
            Test("Unknown enum and numeric strings are never guessed as flags", () => {
                var reader = new TelemetryFlagReader(); bool available;
                Check(!reader.Read(new Flags(), "Enumeration", out available) && !available, "Unknown enum accepted");
                Check(!reader.Read(new Flags(), "Text", out available) && !available, "String accepted");
            });
            Test("Throwing and indexed getters cannot escape the flag reader", () => {
                var reader = new TelemetryFlagReader(); bool available;
                Check(!reader.Read(new Flags(), "Broken", out available) && !available, "Getter escaped");
                Check(!reader.Read(new Flags(), "Item", out available) && !available, "Indexer accepted");
            });
            Test("Nonfinite output-limit settings rejected", () => {
                var s = Full(); s.Brightness = double.NaN; Reject(() => FrameComposer.ApplyOutputLimits(s, Fill(Rgb.White)));
                s = Full(); s.CurrentBudgetAmps = double.PositiveInfinity; Reject(() => FrameComposer.ApplyOutputLimits(s, Fill(Rgb.White)));
            });
            Console.WriteLine("C# core scenarios: " + (count - failed) + " passed / " + failed + " failed / " + count + " total. No Windows capture, SimHub or LED hardware was exercised.");
            return failed == 0 ? 0 : 1;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
