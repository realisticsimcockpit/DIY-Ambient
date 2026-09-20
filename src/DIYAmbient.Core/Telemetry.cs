using System;
using System.Linq;

namespace DIYAmbient.Core
{
    public enum TelemetryEffect { SpotterLeft, SpotterRight, Yellow, Blue, Green, White, Black, Orange, Checkered, Abs, Tc, WheelLock, Rpm }

    public static class TelemetryTestSettings
    {
        // A manual test bypasses only the selected effect's opt-out, never output/power limits.
        public static Settings ForEffect(Settings source, TelemetryEffect effect)
        {
            Settings s = source.Clone();
            switch (effect)
            {
                case TelemetryEffect.SpotterLeft: case TelemetryEffect.SpotterRight: s.TelemetrySpotterEnabled = true; break;
                case TelemetryEffect.Yellow: s.TelemetryYellowEnabled = true; break;
                case TelemetryEffect.Blue: s.TelemetryBlueEnabled = true; break;
                case TelemetryEffect.Green: s.TelemetryGreenEnabled = true; break;
                case TelemetryEffect.White: s.TelemetryWhiteEnabled = true; break;
                case TelemetryEffect.Abs: s.TelemetryAbsEnabled = true; break;
                case TelemetryEffect.Tc: s.TelemetryTcEnabled = true; break;
                case TelemetryEffect.WheelLock: s.TelemetryWheelLockEnabled = true; break;
                default: throw new ArgumentException("Effet de test indisponible.");
            }
            return s;
        }
    }

    public sealed class TelemetrySnapshot
    {
        public readonly bool GameRunning, Left, Right, Yellow, Blue, Green, White, Black, Orange, Checkered;
        public readonly bool Abs, Tc, WheelLock;
        public readonly double RpmPercent;
        public readonly DateTime TimestampUtc;
        private DateTime[] activationTimes;
        private bool Active(TelemetryEffect effect)
        {
            if (!GameRunning) return false;
            switch (effect)
            {
                case TelemetryEffect.SpotterLeft: return Left;
                case TelemetryEffect.SpotterRight: return Right;
                case TelemetryEffect.Yellow: return Yellow;
                case TelemetryEffect.Blue: return Blue;
                case TelemetryEffect.Green: return Green;
                case TelemetryEffect.White: return White;
                case TelemetryEffect.Abs: return Abs;
                case TelemetryEffect.Tc: return Tc;
                case TelemetryEffect.WheelLock: return WheelLock;
                case TelemetryEffect.Rpm: return RpmPercent >= 100;
                default: return false;
            }
        }
        public TelemetrySnapshot WithTiming(TelemetrySnapshot previous)
        {
            var copy = (TelemetrySnapshot)MemberwiseClone();
            copy.activationTimes = new DateTime[13];
            for (int index = 0; index < copy.activationTimes.Length; index++)
            {
                var effect = (TelemetryEffect)index;
                if (!Active(effect)) continue;
                copy.activationTimes[index] = previous != null && previous.activationTimes != null && previous.IsFresh(TimestampUtc) && previous.Active(effect)
                    ? previous.activationTimes[index] : TimestampUtc;
            }
            return copy;
        }
        public bool BlinkOn(TelemetryEffect effect, DateTime now, int halfPeriodMilliseconds)
        {
            if (activationTimes == null) return FrameComposer.TelemetryBlinkOn(now, halfPeriodMilliseconds);
            long elapsed = Math.Max(0, (now - activationTimes[(int)effect]).Ticks / TimeSpan.TicksPerMillisecond);
            return elapsed / halfPeriodMilliseconds % 2 == 0;
        }
        public TelemetrySnapshot(bool running, bool left, bool right, bool yellow, bool blue, DateTime utc)
            : this(running, left, right, yellow, blue, false, false, false, false, false, utc) { }
        public TelemetrySnapshot(bool running, bool left, bool right, bool yellow, bool blue,
            bool green, bool white, bool black, bool orange, bool checkered, DateTime utc)
            : this(running, left, right, yellow, blue, green, white, black, orange, checkered,
                false, false, false, 0, utc) { }
        public TelemetrySnapshot(bool running, bool left, bool right, bool yellow, bool blue,
            bool green, bool white, bool black, bool orange, bool checkered,
            bool abs, bool tc, bool wheelLock, double rpmPercent, DateTime utc)
        {
            GameRunning = running; Left = left; Right = right; Yellow = yellow; Blue = blue;
            Green = green; White = white; Black = black; Orange = orange; Checkered = checkered;
            Abs = abs; Tc = tc; WheelLock = wheelLock;
            RpmPercent = double.IsNaN(rpmPercent) || double.IsInfinity(rpmPercent) ? 0 : Math.Max(0, Math.Min(100, rpmPercent));
            TimestampUtc = utc;
        }
        public bool IsFresh(DateTime now)
        { double age = (now - TimestampUtc).TotalSeconds; return GameRunning && age >= 0 && age <= 1; }
        public static readonly TelemetrySnapshot Empty = new TelemetrySnapshot(false, false, false, false, false, DateTime.MinValue);
    }

    public static class TelemetryMath
    {
        public static bool WheelLock(double brake, double speed, double[] wheelSpeeds)
        {
            if (double.IsNaN(brake) || double.IsInfinity(brake) || brake < .2 ||
                double.IsNaN(speed) || double.IsInfinity(speed) || Math.Abs(speed) < 5 ||
                wheelSpeeds == null || wheelSpeeds.Length == 0) return false;
            double reference = Math.Abs(speed);
            return wheelSpeeds.Any(value => !double.IsNaN(value) && !double.IsInfinity(value) &&
                Math.Abs(value) < reference * .55);
        }
    }

    public sealed class TelemetrySelection
    {
        public readonly int[] Left, Right; // 1-based LED IDs
        public TelemetrySelection(Settings s)
        {
            s.Validate();
            // Telemetry belongs at the two physical ends of the 1 -> 60 route.
            // Screen rectangles are user-editable sampling zones and must never change
            // which LEDs form the contiguous left and right warning blocks.
            var physicalRoute = s.Zones.Select(z => z.Led).OrderBy(id => id).ToArray();
            int half = s.TelemetryLedCount / 2;
            Left = physicalRoute.Take(half).ToArray();
            Right = physicalRoute.Reverse().Take(half).ToArray();
        }
    }
}
