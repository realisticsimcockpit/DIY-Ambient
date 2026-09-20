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
                case AnimationEffect.Blink: Blink(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Breathe: Breathe(frame, primary, ms, settings.AnimationSpeed); break;
                case AnimationEffect.Wipe: Wipe(frame, primary, ms, settings.AnimationSpeed); break;
                case AnimationEffect.Scan: Scan(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Colorloop: Colorloop(frame, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Rainbow: Rainbow(frame, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Theater: Theater(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Chase: Chase(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.Twinkle: Twinkle(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
                case AnimationEffect.FireFlicker: FireFlicker(frame, primary, ms, settings.AnimationSpeed, settings.AnimationIntensity); break;
            }
            return frame;
        }

        private static void Fill(Rgb[] frame, Rgb color)
        { for (int i = 0; i < frame.Length; i++) frame[i] = color; }

        private static void Blink(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            long baseTime = (255 - speed) * 20L;
            long on = 34 + baseTime * intensity / 256;
            long cycle = baseTime + 68;
            Fill(frame, ms % cycle <= on ? color : Rgb.Black);
        }

        private static void Breathe(Rgb[] frame, Rgb color, long ms, int speed)
        {
            long counter = (ms * ((speed >> 3) + 10)) & 65535;
            counter = (counter >> 2) + (counter >> 4);
            double level = 30.0 / 255.0;
            if (counter < 16384)
            {
                if (counter > 8192) counter = 16384 - counter;
                level = (30 + 224 * Math.Sin(counter * Math.PI / 16384.0)) / 255.0;
            }
            Fill(frame, Rgb.Scale(color, level, level, level));
        }

        private static void Wipe(Rgb[] frame, Rgb color, long ms, int speed)
        {
            long cycle = 750L + (255 - speed) * 150L;
            double phase = (ms % cycle) / (double)cycle;
            bool clearing = phase >= .5;
            int edge = (int)((clearing ? (phase - .5) * 2 : phase * 2) * frame.Length);
            for (int i = 0; i < frame.Length; i++) frame[i] = clearing ? (i < edge ? Rgb.Black : color) : (i < edge ? color : Rgb.Black);
        }

        private static void Scan(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            int size = 1 + intensity * frame.Length / 512;
            long cycle = 750L + (255 - speed) * 150L;
            double phase = (ms % cycle) / (double)cycle;
            int start = (int)(Math.Abs(phase * 2 - 1) * Math.Max(0, frame.Length - size));
            for (int i = start; i < start + size && i < frame.Length; i++) frame[i] = color;
        }

        private static void Colorloop(Rgb[] frame, long ms, int speed, int intensity)
        {
            int hue = (int)((ms * ((speed >> 2) + 2) / 256) & 255);
            Rgb color = Wheel(hue);
            if (intensity < 128) color = Blend(Rgb.White, color, intensity * 2);
            Fill(frame, color);
        }

        private static void Rainbow(Rgb[] frame, long ms, int speed, int intensity)
        {
            int offset = (int)((ms * ((speed >> 2) + 2) / 256) & 255);
            int scale = 16 << Math.Min(8, intensity / 29);
            for (int i = 0; i < frame.Length; i++) frame[i] = Wheel((i * scale / frame.Length + offset) & 255);
        }

        private static void Theater(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            int width = 3 + (intensity >> 4);
            int step = (int)((ms / (50L + 255 - speed)) % width);
            for (int i = 0; i < frame.Length; i++) if (i % width == step) frame[i] = color;
        }

        private static void Chase(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            int size = 1 + intensity * frame.Length / 1024;
            int head = (int)((ms / (10L + (30L * (255 - speed) / frame.Length))) % frame.Length);
            for (int n = 0; n < size * 2; n++) frame[(head + n) % frame.Length] = n < size ? color : Rgb.White;
        }

        private static void Twinkle(Rgb[] frame, Rgb color, long ms, int speed, int intensity)
        {
            long slot = ms / (80L + (255 - speed) * 4L);
            int count = 1 + intensity * frame.Length / 512;
            for (int n = 0; n < count; n++)
            {
                int pixel = Hash(slot * 131 + n * 977) % frame.Length;
                int level = 96 + Hash(slot * 67 + n * 313) % 160;
                frame[pixel] = Rgb.Scale(color, level / 255.0, level / 255.0, level / 255.0);
            }
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
