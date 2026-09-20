using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct MonitorInfoEx
        {
            internal int Size;
            internal Rect Monitor, Work;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string DeviceName;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfoHeader
        {
            internal uint Size;
            internal int Width, Height;
            internal ushort Planes, BitCount;
            internal uint Compression, SizeImage;
            internal int XPelsPerMeter, YPelsPerMeter;
            internal uint ClrUsed, ClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfo { internal BitmapInfoHeader Header; internal uint FirstColor; }
        internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr dc, ref Rect bounds, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
        [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateCompatibleDC(IntPtr source);
        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StretchBlt(IntPtr dest, int x, int y, int w, int h,
            IntPtr source, int sx, int sy, int sw, int sh, uint rop);
        [DllImport("gdi32.dll")] internal static extern int SetStretchBltMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetBrushOrgEx(IntPtr dc, int x, int y, IntPtr old);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GdiFlush();
        [DllImport("gdi32.dll")] internal static extern int GetDeviceCaps(IntPtr dc, int index);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }

    internal sealed class DpiScope : IDisposable
    {
        private readonly IntPtr old;
        public DpiScope()
        {
            try { old = Native.SetThreadDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException) { old = IntPtr.Zero; }
        }
        public void Dispose()
        { if (old != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(old); }
    }

    internal sealed class MonitorInfo
    {
        public string DeviceName;
        public Rectangle Bounds;
        public override string ToString()
        { return string.Format("{0} — {1} × {2}", DeviceName.Replace(@"\\.\", ""), Bounds.Width, Bounds.Height); }
        public static MonitorInfo[] Enumerate()
        {
            // Avoid Screen.AllScreens' cached results across different DPI contexts.
            var result = new List<MonitorInfo>();
            int failure = 0;
            Native.MonitorEnumProc callback = delegate(IntPtr monitor, IntPtr dc, ref Native.Rect rect, IntPtr data)
            {
                var info = new Native.MonitorInfoEx();
                info.Size = Marshal.SizeOf(typeof(Native.MonitorInfoEx));
                if (!Native.GetMonitorInfo(monitor, ref info))
                { failure = Marshal.GetLastWin32Error(); return false; }
                if (info.Monitor.Right > info.Monitor.Left && info.Monitor.Bottom > info.Monitor.Top && !string.IsNullOrEmpty(info.DeviceName))
                    result.Add(new MonitorInfo { DeviceName = info.DeviceName,
                        Bounds = Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom) });
                return true;
            };
            using (new DpiScope())
                if (!Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
                    throw new Win32Exception(failure != 0 ? failure : Marshal.GetLastWin32Error(), "Impossible d'énumérer les écrans.");
            GC.KeepAlive(callback);
            return result.ToArray();
        }
    }

    // Replaceable SDR prototype. No DXGI/HDR claim; Windows validation is still required.
    internal sealed class ScreenCapture : IDisposable
    {
        private sealed class Surface : IDisposable
        {
            internal readonly int Width, Height, SourceWidth, SourceHeight;
            internal readonly Rectangle MonitorBounds;
            internal readonly byte[] Buffer;
            private IntPtr source, destination, bitmap, previousBitmap, bits;
            internal Surface(MonitorInfo monitor)
            {
                MonitorBounds = monitor.Bounds;
                try
                {
                    // A source and compatible destination for THIS monitor, not a
                    // desktop DC copied into an unrelated GDI+ bitmap device context.
                    source = Native.CreateDC("DISPLAY", monitor.DeviceName, null, IntPtr.Zero);
                    if (source == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Écran inaccessible.");
                    SourceWidth = Native.GetDeviceCaps(source, 8); // HORZRES
                    SourceHeight = Native.GetDeviceCaps(source, 10); // VERTRES
                    if (SourceWidth < 1 || SourceHeight < 1) throw new InvalidOperationException("Dimensions de capture invalides.");
                    Width = 256; Height = Math.Max(32, Math.Min(512, (int)Math.Round(Width * SourceHeight / (double)SourceWidth)));
                    Buffer = new byte[Width * Height * 4];
                    destination = Native.CreateCompatibleDC(source);
                    if (destination == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    var info = new Native.BitmapInfo();
                    info.Header.Size = (uint)Marshal.SizeOf(typeof(Native.BitmapInfoHeader));
                    info.Header.Width = Width; info.Header.Height = -Height; // top-down, positive packed stride
                    info.Header.Planes = 1; info.Header.BitCount = 32;
                    info.Header.SizeImage = (uint)Buffer.Length;
                    bitmap = Native.CreateDIBSection(source, ref info, 0, out bits, IntPtr.Zero, 0);
                    if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                    previousBitmap = Native.SelectObject(destination, bitmap);
                    if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    if (Native.SetStretchBltMode(destination, 4) == 0 || !Native.SetBrushOrgEx(destination, 0, 0, IntPtr.Zero))
                        throw new InvalidOperationException("Impossible de configurer la réduction de capture.");
                }
                catch { Dispose(); throw; }
            }
            internal void Read()
            {
                if (!Native.StretchBlt(destination, 0, 0, Width, Height, source, 0, 0, SourceWidth, SourceHeight, 0x00CC0020))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                // Required before CPU access to a DIB's bits after GDI drawing.
                if (!Native.GdiFlush()) throw new InvalidOperationException("Synchronisation GDI impossible.");
                Marshal.Copy(bits, Buffer, 0, Buffer.Length);
            }
            public void Dispose()
            {
                if (destination != IntPtr.Zero && previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1))
                    Native.SelectObject(destination, previousBitmap);
                previousBitmap = IntPtr.Zero;
                if (bitmap != IntPtr.Zero) { Native.DeleteObject(bitmap); bitmap = IntPtr.Zero; bits = IntPtr.Zero; }
                if (destination != IntPtr.Zero) { Native.DeleteDC(destination); destination = IntPtr.Zero; }
                if (source != IntPtr.Zero) { Native.DeleteDC(source); source = IntPtr.Zero; }
            }
        }
        private readonly Dictionary<string, Surface> surfaces = new Dictionary<string, Surface>(StringComparer.OrdinalIgnoreCase);
        private MonitorInfo[] monitors = new MonitorInfo[0];
        private Stopwatch refreshAge;
        public string Status { get; private set; }
        public ScreenCapture() { Status = "Capture inactive"; }

        public Rgb[] Capture(Settings settings)
        {
            var output = new Rgb[60];
            var errors = new List<string>();
            using (new DpiScope())
            {
                if (refreshAge == null || refreshAge.ElapsedMilliseconds >= 1000)
                {
                    monitors = MonitorInfo.Enumerate(); refreshAge = Stopwatch.StartNew();
                    foreach (string name in surfaces.Keys.ToArray())
                        if (!monitors.Any(m => string.Equals(m.DeviceName, name, StringComparison.OrdinalIgnoreCase)) ||
                            !settings.Displays.Any(m => string.Equals(m.DeviceName, name, StringComparison.OrdinalIgnoreCase))) Remove(name);
                }
                foreach (DisplayMap map in settings.Displays)
                {
                    try
                    {
                        MonitorInfo monitor = monitors.FirstOrDefault(m => string.Equals(m.DeviceName, map.DeviceName, StringComparison.OrdinalIgnoreCase));
                        if (monitor == null) { errors.Add(map.DeviceName + " absent"); continue; }
                        Surface surface;
                        if (surfaces.TryGetValue(map.DeviceName, out surface) && surface.MonitorBounds != monitor.Bounds)
                        { Remove(map.DeviceName); surface = null; }
                        if (surface == null) { surface = new Surface(monitor); surfaces[map.DeviceName] = surface; }
                        surface.Read();
                        foreach (LedZone zone in settings.Zones.Where(z => string.Equals(z.DeviceName, map.DeviceName, StringComparison.OrdinalIgnoreCase)))
                            output[zone.Led - 1] = ZoneSampler.Average(surface.Buffer, surface.Width, surface.Height, surface.Width * 4, zone);
                    }
                    catch (Exception ex)
                    {
                        // Do not reuse a failed native surface indefinitely after a display change.
                        Remove(map.DeviceName); errors.Add(map.DeviceName + " : " + ex.Message);
                    }
                }
            }
            Status = errors.Count == 0 ? "3 écrans capturés — SDR/GDI expérimental" : string.Join(" ; ", errors);
            return output;
        }
        private void Remove(string name)
        {
            Surface surface;
            if (surfaces.TryGetValue(name, out surface)) { surfaces.Remove(name); surface.Dispose(); }
        }
        public void Dispose()
        { foreach (string name in surfaces.Keys.ToArray()) Remove(name); }
    }
}
