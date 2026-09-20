using System;
using System.Linq;

namespace DIYAmbient.Core
{
    // Pure policies used by the engine and regression tests. No host/port API here.
    public static class OutputPolicy
    {
        public static bool HardwareChanged(Settings before, Settings after)
        {
            return !string.Equals(before.SerialPort, after.SerialPort, StringComparison.OrdinalIgnoreCase) ||
                before.PreviewOnly != after.PreviewOnly ||
                before.ElectricalConfirmed != after.ElectricalConfirmed ||
                before.CurrentBudgetAmps != after.CurrentBudgetAmps;
        }

        public static bool CaptureLayoutEquals(Settings before, Settings after)
        {
            LedZone[] a = before.Zones.OrderBy(z => z.Led).ToArray();
            LedZone[] b = after.Zones.OrderBy(z => z.Led).ToArray();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Led != b[i].Led || !string.Equals(a[i].DeviceName, b[i].DeviceName, StringComparison.OrdinalIgnoreCase) ||
                    a[i].X != b[i].X || a[i].Y != b[i].Y || a[i].Width != b[i].Width || a[i].Height != b[i].Height)
                    return false;
            return true;
        }

        public static bool CanUseCapture(long requiredVersion, long frameVersion, double ageSeconds)
        {
            return requiredVersion == frameVersion && !double.IsNaN(ageSeconds) &&
                ageSeconds >= 0 && ageSeconds < .75;
        }
    }
}
