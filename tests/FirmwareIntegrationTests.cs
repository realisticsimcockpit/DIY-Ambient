using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

class FirmwareIntegrationTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static int Main(string[] args)
    {
        try
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            foreach (string name in new[] { "Adalight_WS2812.ino" })
            {
                using (var stream = assembly.GetManifestResourceStream("DIYAmbient.Firmware." + name))
                    Assert(stream != null && stream.Length > 500, "Missing embedded firmware " + name);
            }
            Type engineType = assembly.GetType("DIYAmbient.Plugin.AmbientEngine", true);
            Type settingsType = assembly.GetType("DIYAmbient.Core.Settings", true);
            object settings = Activator.CreateInstance(settingsType);
            settingsType.GetField("SerialPort").SetValue(settings, ""); // Never access hardware.
            var engine = (IDisposable)Activator.CreateInstance(engineType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { settings }, null);
            MethodInfo setRunning = engineType.GetMethod("SetGameRunning", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo stateProperty = engineType.GetProperty("State", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            setRunning.Invoke(engine, new object[] { true });
            object runningState = stateProperty.GetValue(engine, null);
            Assert((bool)runningState.GetType().GetField("GameRunning").GetValue(runningState), "Engine did not enter game mode");
            setRunning.Invoke(engine, new object[] { false });
            object idleState = stateProperty.GetValue(engine, null);
            Assert(!(bool)idleState.GetType().GetField("GameRunning").GetValue(idleState), "Engine did not return to idle mode");
            MethodInfo exclusive = engineType.GetMethod("WithFirmwarePort", BindingFlags.Static | BindingFlags.NonPublic);
            var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
            Task lease = Task.Run(() => exclusive.Invoke(null, new object[] { (Action)(() => { entered.Set(); Assert(release.Wait(5000), "lease release timeout"); }) }));
            Assert(entered.Wait(5000), "Output worker did not yield ownership");
            bool rejected = false;
            try { exclusive.Invoke(null, new object[] { (Action)(() => { throw new Exception("Second uploader entered!"); }) }); }
            catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
            Assert(rejected, "Concurrent uploader must be rejected");
            engine.Dispose(); // Shutdown during maintenance cannot hang or reacquire the port.
            release.Set(); Assert(lease.Wait(5000), "Lease did not finish");
            try { exclusive.Invoke(null, new object[] { (Action)(() => { throw new IOException("Expected uploader failure"); }) }); }
            catch (TargetInvocationException ex) { Assert(ex.InnerException is IOException, "Unexpected failure"); }
            bool ran = false; exclusive.Invoke(null, new object[] { (Action)(() => ran = true) });
            Assert(ran, "Failure leaked firmware lock");
            Type flasher = assembly.GetType("DIYAmbient.Plugin.FirmwareFlasher", true);
            MethodInfo compile = flasher.GetMethod("CompileOnly", BindingFlags.Static | BindingFlags.NonPublic);
            try { compile.Invoke(null, new object[] { -1, (Action<string>)Console.WriteLine, null }); throw new Exception("Invalid board accepted"); }
            catch (TargetInvocationException ex) { Assert(ex.InnerException is ArgumentException, "Wrong invalid-board error"); }
            // Explicit opt-in compiles embedded resources for both Nano bootloaders. NEVER uploads.
            if (args.Length > 1 && args[1] == "--compile")
                foreach (int board in new[] { 1, 2 }) compile.Invoke(null, new object[] { board, (Action<string>)Console.WriteLine, null });
            Console.WriteLine("PASS firmware resources, exclusive ownership, concurrent rejection, shutdown, failure cleanup, board validation. No serial port or upload exercised.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
