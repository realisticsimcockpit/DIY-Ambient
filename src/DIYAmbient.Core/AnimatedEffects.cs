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
                case AnimationEffect.Loading: Loading(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity, settings.AnimationRandomPalette); break;
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

        private static void Loading(Rgb[] frame, Rgb color, long ms, int speed, int intensity, bool randomPalette)
        {
            int counter = (int)((ms * ((speed >> 2) + 1)) & 65535);
            int point = counter * frame.Length >> 16;
            int fade = 1 + intensity / 2;
            int wrappedPoint = point + frame.Length;
            for (int i = 0; i < frame.Length; i++)
            {
                int distance = Math.Abs((i > point ? wrappedPoint : point) - i);
                int amount = fade > distance ? distance * 255 / fade : 255;
                Rgb background = randomPalette ? RandomCyclePalette(ms, i, frame.Length) : Rgb.Black;
                frame[i] = Blend(color, background, amount);
            }
        }

        private static Rgb RandomCyclePalette(long ms, int pixel, int length)
        {
            const long cycle = 5000;
            long slot = ms / cycle;
            int current = Hash(slot * 7919) % 6;
            int next = Hash((slot + 1) * 7919) % 6;
            if (next == current) next = (next + 1) % 6;
            int position = length <= 1 ? 0 : pixel * 255 / (length - 1);
            int mix = (int)((ms % cycle) * 255 / cycle);
            return Blend(Palette(current, position), Palette(next, position), mix);
        }

        private static Rgb Palette(int palette, int position)
        {
            switch (palette)
            {
                case 0: return Wheel(position);
                case 1: return Gradient(position, new Rgb(0, 5, 35), new Rgb(0, 85, 150), new Rgb(0, 200, 185), Rgb.White);
                case 2: return Gradient(position, new Rgb(20, 0, 0), new Rgb(180, 0, 0), new Rgb(255, 100, 0), new Rgb(255, 235, 80));
                case 3: return Gradient(position, new Rgb(0, 20, 0), new Rgb(0, 120, 20), new Rgb(150, 210, 20), new Rgb(20, 80, 0));
                case 4: return Gradient(position, new Rgb(12, 0, 45), new Rgb(120, 0, 130), new Rgb(255, 40, 35), new Rgb(255, 175, 20));
                default: return Gradient(position, new Rgb(255, 0, 110), new Rgb(255, 120, 0), new Rgb(30, 0, 255), new Rgb(0, 210, 170));
            }
        }

        private static Rgb Gradient(int position, Rgb a, Rgb b, Rgb c, Rgb d)
        {
            if (position < 85) return Blend(a, b, position * 3);
            if (position < 170) return Blend(b, c, (position - 85) * 3);
            return Blend(c, d, (position - 170) * 3);
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
