using System;

namespace DIYAmbient.Core
{
    public static class CapturePacing
    {
        public const int FrameIntervalMilliseconds = 34;
        // Processing time belongs inside the frame budget, not ahead of another full sleep.
        public static int RemainingMilliseconds(long elapsedMilliseconds)
        { return (int)Math.Max(1, FrameIntervalMilliseconds - Math.Min(FrameIntervalMilliseconds, Math.Max(0, elapsedMilliseconds))); }
    }
}
