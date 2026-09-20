using System;

namespace DIYAmbient.Core
{
    public static class ZoneSampler
    {
        // BGRA32, top-to-bottom rows. Coordinates are local to one monitor, normalized.
        public static Rgb Average(byte[] bgra, int width, int height, int stride, LedZone zone)
        {
            // Long intermediates: malformed dimensions must not overflow validation.
            if (bgra == null || width < 1 || height < 1 || stride < (long)width * 4 ||
                (long)bgra.Length < (long)stride * height)
                throw new ArgumentException("Image BGRA invalide.");
            if (zone == null || !Finite(zone.X) || !Finite(zone.Y) || !Finite(zone.Width) || !Finite(zone.Height) ||
                zone.X < 0 || zone.Y < 0 || zone.Width <= 0 || zone.Height <= 0 ||
                zone.X + zone.Width > 1.0000001 || zone.Y + zone.Height > 1.0000001)
                throw new ArgumentException("Rectangle de capture invalide.");
            int x0 = Math.Max(0, Math.Min(width - 1, (int)Math.Floor(zone.X * width)));
            int y0 = Math.Max(0, Math.Min(height - 1, (int)Math.Floor(zone.Y * height)));
            int x1 = Math.Min(width, Math.Max(x0 + 1, (int)Math.Ceiling((zone.X + zone.Width) * width)));
            int y1 = Math.Min(height, Math.Max(y0 + 1, (int)Math.Ceiling((zone.Y + zone.Height) * height)));
            long r = 0, g = 0, b = 0, n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int p = y * stride + x * 4;
                    b += bgra[p]; g += bgra[p + 1]; r += bgra[p + 2]; n++;
                }
            return new Rgb((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }
        private static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
    }
}
