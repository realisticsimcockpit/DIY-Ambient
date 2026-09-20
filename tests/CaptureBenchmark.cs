using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

class CaptureBenchmark
{
    static int Main(string[] args)
    {
        IDisposable capture = null;
        try
        {
            var assembly = Assembly.LoadFrom(System.IO.Path.GetFullPath(args[0]));
            var storage = assembly.GetType("DIYAmbient.Plugin.Storage", true);
            object[] load = { null };
            object settings = storage.GetMethod("Load").Invoke(null, load);
            if (!string.IsNullOrEmpty(load[0] as string)) throw new Exception((string)load[0]);
            Type type = assembly.GetType("DIYAmbient.Plugin.ScreenCapture", true);
            capture = (IDisposable)Activator.CreateInstance(type, true);
            var method = type.GetMethod("Capture");
            var durations = new double[60];
            for (int i = -3; i < durations.Length; i++)
            {
                var watch = Stopwatch.StartNew();
                var pixels = (Array)method.Invoke(capture, new[] { settings });
                if (pixels.Length != 60) throw new Exception("Wrong LED count");
                if (i >= 0) durations[i] = watch.Elapsed.TotalMilliseconds;
                Thread.Sleep(34);
            }
            Console.WriteLine("Real Windows capture, no serial output: mean={0:F2} ms p95={1:F2} ms max={2:F2} ms / 60 samples", durations.Average(), durations.OrderBy(v => v).ElementAt(56), durations.Max());
            Console.WriteLine(type.GetProperty("Status").GetValue(capture, null));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (capture != null) capture.Dispose(); }
    }
}
