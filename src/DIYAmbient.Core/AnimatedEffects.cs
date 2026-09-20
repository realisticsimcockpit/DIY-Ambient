using System;

namespace DIYAmbient.Core
{
    // Independent, deterministic implementation of familiar WLED-style 1D effects.
    // No WLED source code is included; names describe visual compatibility only.
    public static class AnimatedEffects
    {
        public static Rgb[] Compose(Settings settings, DateTime now)
        {
            var frame = new Rgb[Settings.LedCount];
            long ms = now.Ticks / TimeSpan.TicksPerMillisecond;
            Rgb primary = new Rgb(settings.SolidR, settings.SolidG, settings.SolidB);
            switch (settings.AnimationEffect)
            {
                case AnimationEffect.Colorloop: Colorloop(frame, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Rainbow: Rainbow(frame, ms, settings.AnimationSpeed); break;
                case AnimationEffect.FireFlicker: FireFlicker(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Loading: Loading(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
            }
            return frame;
        }

        private static void Fill(Rgb[] frame, Rgb color)
        { for (int i = 0; i < frame.Length; i++) frame[i] = color; }

        private static void Colorloop(Rgb[] frame, long ms, int speed, int intensity)
        {
            int hue = (int)((ms * ((speed >> 2) + 2) / 256) & 255);
            Rgb color = Wheel(hue);
            if (intensity < 128) color = Blend(Rgb.White, color, intensity * 2);
            Fill(frame, color);
        }

        private static void Rainbow(Rgb[] frame, long ms, int speed)
        {
            int offset = (int)((ms * ((speed >> 2) + 2) / 256) & 255);
            // One complete rainbow is always spread over the fixed 60-LED route.
            for (int i = 0; i < frame.Length; i++) frame[i] = Wheel((i * 256 / frame.Length + offset) & 255);
        }

        private static void FireFlicker(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            long slot = ms / (40L + 255 - speed);
            int max = Math.Max(1, Math.Max(color.R, Math.Max(color.G, color.B)) / (((256 - intensity) / 16) + 1));
            for (int i = 0; i < frame.Length; i++)
            {
                int flicker = Hash(slot * 257 + i * 8191) % max;
                frame[i] = new Rgb((byte)Math.Max(0, color.R - flicker), (byte)Math.Max(0, color.G - flicker), (byte)Math.Max(0, color.B - flicker));
            }
        }

        private static void Loading(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            int counter = (int)((ms * ((speed >> 2) + 1)) & 65535);
            int point = counter * frame.Length >> 16;
            int fade = 1 + intensity / 2;
            int wrappedPoint = point + frame.Length;
            for (int i = 0; i < frame.Length; i++)
            {
                int distance = Math.Abs((i > point ? wrappedPoint : point) - i);
                int amount = fade > distance ? distance * 255 / fade : 255;
                frame[i] = Blend(color, Rgb.Black, amount);
            }
        }

        private static int Hash(long value)
        {
            unchecked
            {
                uint x = (uint)(value ^ (value >> 32));
                x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
                return (int)(x & 0x7fffffff);
            }
        }

        private static Rgb Blend(Rgb a, Rgb b, int amount)
        {
            amount = Math.Max(0, Math.Min(255, amount)); int inv = 255 - amount;
            return new Rgb((byte)((a.R * inv + b.R * amount) / 255), (byte)((a.G * inv + b.G * amount) / 255), (byte)((a.B * inv + b.B * amount) / 255));
        }

        private static Rgb Wheel(int position)
        {
            position = 255 - (position & 255);
            if (position < 85) return new Rgb((byte)(255 - position * 3), 0, (byte)(position * 3));
            if (position < 170) { position -= 85; return new Rgb(0, (byte)(position * 3), (byte)(255 - position * 3)); }
            position -= 170; return new Rgb((byte)(position * 3), (byte)(255 - position * 3), 0);
        }
    }
}
