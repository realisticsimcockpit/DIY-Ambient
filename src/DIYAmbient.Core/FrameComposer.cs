using System;
using System.Linq;

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
        public const int TelemetryBlinkHalfPeriodMilliseconds = 250;
        public const int GreenFlagBlinkHalfPeriodMilliseconds = 1000;

        public static bool TelemetryBlinkOn(DateTime now)
        { return TelemetryBlinkOn(now, TelemetryBlinkHalfPeriodMilliseconds); }

        public static bool TelemetryBlinkOn(DateTime now, int halfPeriodMilliseconds)
        {
            long halfPeriods = now.Ticks / TimeSpan.TicksPerMillisecond / halfPeriodMilliseconds;
            return halfPeriods % 2 == 0;
        }

        public static FrameResult Compose(Settings s, bool enabled, Rgb[] screen,
            TelemetrySnapshot telemetry, TelemetrySelection selection, DateTime now, int identifyLed)
        {
            var colors = new Rgb[Settings.LedCount];
            if (!enabled) return new FrameResult(colors, Estimate(colors, s.LedStripCount), 1);
            Rgb solid = new Rgb(s.SolidR, s.SolidG, s.SolidB);
            Rgb white = WhiteBalance(s);
            Rgb[] animation = s.Mode == LightingMode.Animation ? AnimatedEffects.Compose(s, now) : null;
            for (int i = 0; i < colors.Length; i++)
                colors[i] = s.Mode == LightingMode.White ? white :
                    s.Mode == LightingMode.Solid ? solid :
                    s.Mode == LightingMode.Animation ? animation[i] :
                    s.Mode == LightingMode.Rpm ? RpmColor(telemetry, now) :
                    screen != null && screen.Length == 60 ? screen[i] : Rgb.Black;

            if (s.TelemetryLedCount > 0 && telemetry != null && telemetry.IsFresh(now))
            {
                int[] both = selection.Left.Concat(selection.Right).ToArray();
                // Lowest priority first. A higher-priority flag or the spotter may replace it.
                if (s.TelemetryGreenEnabled && telemetry.Green && telemetry.BlinkOn(TelemetryEffect.Green, now, GreenFlagBlinkHalfPeriodMilliseconds)) Paint(colors, both, new Rgb(0, 255, 50));
                if (s.TelemetryWhiteEnabled && telemetry.White) Paint(colors, both, Rgb.White);
                if (s.TelemetryBlueEnabled && telemetry.Blue && FastBlink(telemetry, TelemetryEffect.Blue, now)) Paint(colors, both, new Rgb(0, 70, 255));
                if (s.TelemetryYellowEnabled && telemetry.Yellow && FastBlink(telemetry, TelemetryEffect.Yellow, now)) Paint(colors, both, new Rgb(255, 160, 0));
                if (s.TelemetryAbsEnabled && telemetry.Abs && FastBlink(telemetry, TelemetryEffect.Abs, now)) Paint(colors, both, new Rgb(255, 70, 0));
                if (s.TelemetryTcEnabled && telemetry.Tc && FastBlink(telemetry, TelemetryEffect.Tc, now)) Paint(colors, both, new Rgb(210, 0, 255));
                if (s.TelemetryWheelLockEnabled && telemetry.WheelLock && FastBlink(telemetry, TelemetryEffect.WheelLock, now)) Paint(colors, both, new Rgb(255, 0, 0));
                if (s.TelemetrySpotterEnabled && telemetry.Left && FastBlink(telemetry, TelemetryEffect.SpotterLeft, now)) Paint(colors, selection.Left, SpotterRgb(s.SpotterColor));
                if (s.TelemetrySpotterEnabled && telemetry.Right && FastBlink(telemetry, TelemetryEffect.SpotterRight, now)) Paint(colors, selection.Right, SpotterRgb(s.SpotterColor));
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
        private static bool FastBlink(TelemetrySnapshot telemetry, TelemetryEffect effect, DateTime now)
        { return telemetry.BlinkOn(effect, now, TelemetryBlinkHalfPeriodMilliseconds); }

        public static Rgb SpotterRgb(SpotterColor color)
        {
            switch (color) {
                case SpotterColor.Orange: return new Rgb(255, 70, 0);
                case SpotterColor.Purple: return new Rgb(160, 0, 255);
                case SpotterColor.Pink: return new Rgb(255, 0, 120);
                case SpotterColor.White: return Rgb.White;
                default: return new Rgb(255, 0, 0);
            }
        }

        private static Rgb RpmColor(TelemetrySnapshot telemetry, DateTime now)
        {
            if (telemetry == null || !telemetry.IsFresh(now) || telemetry.RpmPercent <= 0) return Rgb.Black;
            double fraction = telemetry.RpmPercent / 100.0;
            if (fraction >= 1) return telemetry.BlinkOn(TelemetryEffect.Rpm, now, TelemetryBlinkHalfPeriodMilliseconds) ? new Rgb(255, 0, 0) : Rgb.Black;
            return new Rgb((byte)Math.Round(255 * Math.Min(1, fraction * 2)),
                (byte)Math.Round(255 * Math.Min(1, (1 - fraction) * 2)), 0);
        }

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
                s.CurrentBudgetAmps < Settings.LedCount * s.LedStripCount * IdleAmpsPerLed ||
                double.IsNaN(s.Brightness) || double.IsInfinity(s.Brightness) || s.Brightness < 0 || s.Brightness > 1)
                throw new ArgumentException("Budget ou luminosité invalide.");
            // FIXED ceiling based on the worst modeled frame (60 full RGB whites).
            // Do not recompute a larger gain when a flag/spotter replaces some whites:
            // that made unrelated LEDs brighten and caused image-dependent pumping.
            double idle = Settings.LedCount * s.LedStripCount * IdleAmpsPerLed;
            double maximumDynamic = Settings.LedCount * s.LedStripCount * 3 * ChannelAmps;
            double scale = Math.Min(1, Math.Max(0, (s.CurrentBudgetAmps - idle) / maximumDynamic));
            double effective = scale * s.Brightness;
            var result = new Rgb[Settings.LedCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = Rgb.Scale(input[i], effective, effective, effective);
            return new FrameResult(result, Estimate(result, s.LedStripCount), scale);
        }

        public static double Estimate(Rgb[] colors)
        { return Estimate(colors, 1); }

        public static double Estimate(Rgb[] colors, int stripCount)
        {
            double amps = colors.Length * IdleAmpsPerLed;
            foreach (Rgb c in colors) amps += ChannelAmps * (c.R + c.G + c.B) / 255.0;
            return amps * stripCount;
        }
    }
}
