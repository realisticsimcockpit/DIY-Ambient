using System;

namespace DIYAmbient.Core
{
    public sealed class FrameResult
    {
        public readonly Rgb[] Colors;
        public readonly double EstimatedAmps, PowerScale;
        public FrameResult(Rgb[] colors, double estimatedAmps, double powerScale)
        { Colors = colors; EstimatedAmps = estimatedAmps; PowerScale = powerScale; }
    }

    public static class FrameComposer
    {
        // Conservative *model*, not measured current. Controller consumption excluded.
        public const double IdleAmpsPerLed = .001;
        public const double ChannelAmps = .020;

        public static FrameResult Compose(Settings s, bool enabled, Rgb[] screen,
            TelemetrySnapshot telemetry, TelemetrySelection selection, DateTime now, int identifyLed)
        {
            var colors = new Rgb[Settings.LedCount];
            if (!enabled) return new FrameResult(colors, Estimate(colors), 1);
            Rgb solid = new Rgb(s.SolidR, s.SolidG, s.SolidB);
            Rgb white = WhiteBalance(s);
            for (int i = 0; i < colors.Length; i++)
                colors[i] = s.Mode == LightingMode.White ? white :
                    s.Mode == LightingMode.Solid ? solid : screen != null && screen.Length == 60 ? screen[i] : Rgb.Black;

            if (s.TelemetryLedCount > 0 && telemetry != null && telemetry.IsFresh(now))
            {
                // Alert background affects only selected telemetry LEDs. Spotter overrides flags.
                if (telemetry.Yellow || telemetry.Blue)
                {
                    Rgb flag = telemetry.Yellow ? new Rgb(255, 160, 0) : new Rgb(0, 70, 255);
                    Paint(colors, selection.Left, flag); Paint(colors, selection.Right, flag);
                }
                if (telemetry.Left) Paint(colors, selection.Left, new Rgb(255, 0, 0));
                if (telemetry.Right) Paint(colors, selection.Right, new Rgb(255, 0, 0));
            }
            if (identifyLed >= 1 && identifyLed <= 60)
            {
                Array.Clear(colors, 0, colors.Length);
                colors[identifyLed - 1] = white;
            }
            return ApplyOutputLimits(s, colors);
        }

        private static void Paint(Rgb[] frame, int[] ids, Rgb color)
        { foreach (int id in ids) frame[id - 1] = color; }

        public static Rgb WhiteBalance(Settings s)
        {
            // A saved white preset only. Screen pixels, solid RGB and alerts are NOT tinted.
            double r = 1 + .25 * s.Warmth, b = 1 - .25 * s.Warmth, g = 1 + .25 * s.Tint;
            double max = Math.Max(r, Math.Max(g, b));
            return Rgb.Scale(Rgb.White, r / max, g / max, b / max);
        }

        public static FrameResult ApplyOutputLimits(Settings s, Rgb[] input)
        {
            if (input == null || input.Length != Settings.LedCount)
                throw new ArgumentException("60 couleurs requises.");
            if (double.IsNaN(s.CurrentBudgetAmps) || double.IsInfinity(s.CurrentBudgetAmps) ||
                s.CurrentBudgetAmps < Settings.LedCount * IdleAmpsPerLed ||
                double.IsNaN(s.Brightness) || double.IsInfinity(s.Brightness) || s.Brightness < 0 || s.Brightness > 1)
                throw new ArgumentException("Budget ou luminosité invalide.");
            // FIXED ceiling based on the worst modeled frame (60 full RGB whites).
            // Do not recompute a larger gain when a flag/spotter replaces some whites:
            // that made unrelated LEDs brighten and caused image-dependent pumping.
            double idle = Settings.LedCount * IdleAmpsPerLed;
            double maximumDynamic = Settings.LedCount * 3 * ChannelAmps;
            double scale = Math.Min(1, Math.Max(0, (s.CurrentBudgetAmps - idle) / maximumDynamic));
            double effective = scale * s.Brightness;
            var result = new Rgb[Settings.LedCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = Rgb.Scale(input[i], effective, effective, effective);
            return new FrameResult(result, Estimate(result), scale);
        }

        public static double Estimate(Rgb[] colors)
        {
            double amps = colors.Length * IdleAmpsPerLed;
            foreach (Rgb c in colors) amps += ChannelAmps * (c.R + c.G + c.B) / 255.0;
            return amps;
        }
    }
}
