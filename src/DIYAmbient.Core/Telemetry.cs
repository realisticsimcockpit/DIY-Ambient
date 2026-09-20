using System;
using System.Linq;

namespace DIYAmbient.Core
{
    public sealed class TelemetrySnapshot
    {
        public readonly bool GameRunning, Left, Right, Yellow, Blue;
        public readonly DateTime TimestampUtc;
        public TelemetrySnapshot(bool running, bool left, bool right, bool yellow, bool blue, DateTime utc)
        { GameRunning = running; Left = left; Right = right; Yellow = yellow; Blue = blue; TimestampUtc = utc; }
        public bool IsFresh(DateTime now)
        { double age = (now - TimestampUtc).TotalSeconds; return GameRunning && age >= 0 && age <= 1; }
        public static readonly TelemetrySnapshot Empty = new TelemetrySnapshot(false, false, false, false, false, DateTime.MinValue);
    }

    public sealed class TelemetrySelection
    {
        public readonly int[] Left, Right; // 1-based LED IDs
        public TelemetrySelection(Settings s)
        {
            s.Validate();
            // Derive physical side from the user's display roles and positioned rectangles,
            // never from Windows monitor numbers or a hardcoded LED 1 == left assumption.
            var rank = s.Zones.OrderBy(z =>
                s.Displays.Single(d => string.Equals(d.DeviceName, z.DeviceName, StringComparison.OrdinalIgnoreCase)).Position + z.X + z.Width / 2)
                .ThenBy(z => z.Y + z.Height / 2).ThenBy(z => z.Led).Select(z => z.Led).ToArray();
            int half = s.TelemetryLedCount / 2;
            Left = rank.Take(half).ToArray();
            Right = rank.Reverse().Take(half).ToArray();
        }
    }
}
