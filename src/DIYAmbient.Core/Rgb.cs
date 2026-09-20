using System;

namespace DIYAmbient.Core
{
    public struct Rgb
    {
        public readonly byte R, G, B;
        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }
        public static readonly Rgb Black = new Rgb(0, 0, 0);
        public static readonly Rgb White = new Rgb(255, 255, 255);
        public static Rgb Scale(Rgb c, double r, double g, double b)
        {
            return new Rgb(ToByte(c.R * r), ToByte(c.G * g), ToByte(c.B * b));
        }
        public static byte ToByte(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
            // Floor deliberately: rounding up could exceed the estimated current budget.
            return (byte)Math.Max(0, Math.Min(255, Math.Floor(x)));
        }
        public override string ToString() { return string.Format("#{0:X2}{1:X2}{2:X2}", R, G, B); }
    }
}
