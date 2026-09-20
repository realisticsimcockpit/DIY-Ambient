using System;
using System.Collections.Generic;
using System.Linq;

namespace DIYAmbient.Core
{
    public static class ZoneLayout
    {
        // Measured from the user's reference strip 1 and the spacing of strips 1/2.
        public const double StripWidth = 0.023515625;
        public const double StripHeight = 0.37347222222222221;
        public const double StripGap = 0.000140625;

        public static IEnumerable<LedZone> CenteredStrips(DisplayMap display)
        {
            int count = display.LastLed - display.FirstLed + 1;
            double width = Math.Min(StripWidth, (1 - StripGap * (count - 1)) / count);
            double span = count * width + (count - 1) * StripGap;
            for (int i = 0; i < count; i++)
                yield return new LedZone { Led = display.FirstLed + i, DeviceName = display.DeviceName,
                    X = (1 - span) / 2 + i * (width + StripGap), Y = (1 - StripHeight) / 2,
                    Width = width, Height = StripHeight };
        }

        // Clamp one shared delta, never each strip separately: preserve group spacing.
        public static void MoveGroup(IList<LedZone> targets, IList<LedZone> origins, double dx, double dy)
        {
            if (targets.Count == 0) return;
            dx = Math.Max(-origins.Min(z => z.X), Math.Min(1 - origins.Max(z => z.X + z.Width), dx));
            dy = Math.Max(-origins.Min(z => z.Y), Math.Min(1 - origins.Max(z => z.Y + z.Height), dy));
            for (int i = 0; i < targets.Count; i++) { targets[i].X = origins[i].X + dx; targets[i].Y = origins[i].Y + dy; }
        }
    }
}
