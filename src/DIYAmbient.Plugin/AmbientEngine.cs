using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using DIYAmbient.Core;

namespace DIYAmbient.Plugin
{
    internal sealed class EngineState
    {
        public readonly Settings Settings;
        public readonly TelemetrySelection Selection;
        public readonly bool Enabled, Editing;
        public readonly long CaptureVersion;
        public EngineState(Settings settings, bool enabled, bool editing, long captureVersion)
        {
            Settings = settings; Selection = new TelemetrySelection(settings);
            Enabled = enabled; Editing = editing; CaptureVersion = captureVersion;
        }
    }
    internal sealed class CapturedFrame
    {
        public readonly Rgb[] Colors;
        public readonly DateTime At;
        public readonly string Status;
        public readonly long Version;
        public CapturedFrame(Rgb[] colors, string status, long version, DateTime at)
        { Colors = colors; Status = status; Version = version; At = at; }
    }
    internal sealed class TestRequest
    {
        public readonly bool Left, Right, Yellow, Blue, Green, White, Black, Orange, Checkered, Abs, Tc, WheelLock;
        public readonly double RpmPercent;
        public readonly int IdentifyLed;
        public readonly TelemetryEffect? Effect;
        public TelemetrySnapshot Previous; // Owned by the output worker only.
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        public TestRequest(bool left, bool right, int id, bool yellow)
        { Left = left; Right = right; IdentifyLed = id; Yellow = yellow; }
        public TestRequest(TelemetryEffect effect)
        {
            Effect = effect;
            Left = effect == TelemetryEffect.SpotterLeft; Right = effect == TelemetryEffect.SpotterRight;
            Yellow = effect == TelemetryEffect.Yellow; Blue = effect == TelemetryEffect.Blue;
            Green = effect == TelemetryEffect.Green; White = effect == TelemetryEffect.White;
            Black = effect == TelemetryEffect.Black; Orange = effect == TelemetryEffect.Orange;
            Checkered = effect == TelemetryEffect.Checkered;
            Abs = effect == TelemetryEffect.Abs; Tc = effect == TelemetryEffect.Tc;
            WheelLock = effect == TelemetryEffect.WheelLock; RpmPercent = effect == TelemetryEffect.Rpm ? 75 : 0;
        }
        public bool Active { get { return elapsed.ElapsedMilliseconds < 3000; } }
    }

    internal sealed class AmbientEngine : IDisposable
    {
        // SimHub may recreate plugins at a game change. A worker still closing its
        // port must finish before any new instance may become a serial writer.
        private static readonly object OutputOwner = new object();
        private static int firmwareBusy;
        internal static bool FirmwareBusy { get { return Interlocked.CompareExchange(ref firmwareBusy, 0, 0) != 0; } }
        internal static void WithFirmwarePort(Action upload)
        {
            if (Interlocked.CompareExchange(ref firmwareBusy, 1, 0) != 0)
                throw new InvalidOperationException("Une opération firmware est déjà en cours.");
            bool acquired = false;
            try
            {
                acquired = Monitor.TryEnter(OutputOwner, 10000);
                if (!acquired) throw new TimeoutException("Le port n'a pas été libéré. Aucun flash lancé.");
                upload();
            }
            finally { if (acquired) Monitor.Exit(OutputOwner); Interlocked.Exchange(ref firmwareBusy, 0); }
        }
        private readonly object gate = new object();
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly AutoResetEvent outputWake = new AutoResetEvent(false);
        private readonly Task captureTask, outputTask;
        private volatile EngineState state;
        private volatile TelemetrySnapshot telemetry = TelemetrySnapshot.Empty;
        private volatile CapturedFrame captured;
        private volatile TestRequest test;
        private volatile FrameResult lastFrame = new FrameResult(new Rgb[60], .06, 1);
        private volatile string status = "Éteint — aucun port ouvert";
        private volatile string telemetryStatus = "Aucune télémétrie";
        private SerialPort serial; // Only the output worker may access this object.
        private bool evoFirmware;
        private volatile string firmwareStatus = "Firmware non identifié";
        public string FirmwareStatus { get { return firmwareStatus; } }
        private Stopwatch retryDelay;
        private Stopwatch lastWriteAt;
        private bool disposed;
        private volatile bool keepOnAfterExit;
        private volatile bool startupBlackoutPending;

        public AmbientEngine(Settings settings)
        {
            settings.Validate(); state = new EngineState(settings.Clone(), false, false, 0);
            startupBlackoutPending = !settings.StartEnabled && !string.IsNullOrWhiteSpace(settings.SerialPort);
            captureTask = Task.Factory.StartNew(CaptureLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            outputTask = Task.Factory.StartNew(OutputLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        public EngineState State { get { return state; } }
        public FrameResult LastFrame { get { return lastFrame; } }
        public string Status { get { return status; } }
        public string TelemetryStatus { get { return telemetryStatus; } }
        public string CaptureStatus
        {
            get
            {
                EngineState current = state;
                if (!current.Enabled || current.Editing || current.Settings.Mode != LightingMode.Screen) return "Capture inactive";
                CapturedFrame frame = captured;
                return frame == null || frame.Version != current.CaptureVersion ? "En attente d'une nouvelle capture" : frame.Status;
            }
        }

        public void Apply(Settings settings)
        {
            Settings copy = settings.Clone(); copy.Validate();
            lock (gate)
            {
                EnsureActive();
                bool disarm = OutputPolicy.HardwareChanged(state.Settings, copy);
                bool invalidate = disarm || state.Settings.Mode != copy.Mode || !OutputPolicy.CaptureLayoutEquals(state.Settings, copy);
                long version = state.CaptureVersion + (invalidate ? 1 : 0);
                state = new EngineState(copy, state.Enabled && !disarm, state.Editing, version);
                if (invalidate) captured = null;
                if (disarm) { test = null; lastFrame = new FrameResult(new Rgb[60], .06, 1); }
                if (!state.Enabled && !string.IsNullOrWhiteSpace(copy.SerialPort)) startupBlackoutPending = true;
            }
        }
        public void SetEnabled(bool enabled)
        {
            lock (gate)
            {
                EnsureActive();
                Settings s = state.Settings;
                if (enabled && string.IsNullOrWhiteSpace(s.SerialPort))
                    throw new InvalidOperationException("Sélectionner le port avant d'activer l'éclairage.");
                if (enabled == state.Enabled) return;
                state = new EngineState(s, enabled, state.Editing, state.CaptureVersion + 1);
                captured = null; test = null;
                if (!enabled) lastFrame = new FrameResult(new Rgb[60], .06, 1);
            }
        }
        public void SetEditing(bool editing)
        {
            lock (gate)
            {
                EnsureActive();
                state = new EngineState(state.Settings, state.Enabled, editing, state.CaptureVersion + 1);
                captured = null; test = null;
            }
        }
        public void ClearTest() { test = null; }
        public void SetTelemetry(TelemetrySnapshot snapshot, string description)
        {
            lock (gate)
            {
                if (disposed) return;
                telemetry = (snapshot ?? TelemetrySnapshot.Empty).WithTiming(telemetry);
                telemetryStatus = description;
            }
        }
        public void Test(bool left, bool right, int identifyLed)
        {
            lock (gate)
            {
                EnsureActive();
                if (!state.Enabled) throw new InvalidOperationException("Activer l'éclairage avant un test.");
                if (identifyLed < 0 || identifyLed > Settings.LedCount) throw new ArgumentException("Numéro de LED invalide.");
                if (identifyLed == 0 && state.Settings.TelemetryLedCount == 0)
                    throw new InvalidOperationException("Choisir au moins 2 LED de télémétrie avant ce test.");
                test = new TestRequest(left, right, identifyLed, false);
            }
        }
        public void TestTelemetry(TelemetryEffect effect)
        {
            lock (gate)
            {
                EnsureActive();
                if (!state.Enabled) throw new InvalidOperationException("Activer l'éclairage avant le test.");
                if (state.Settings.TelemetryLedCount == 0) throw new InvalidOperationException("Choisir au moins 2 LED de télémétrie.");
                test = new TestRequest(effect);
            }
        }
        private void EnsureActive() { if (disposed) throw new ObjectDisposedException("DIY Ambient light EVO"); }

        private static void CloseCapture(ref ScreenCapture capture)
        {
            if (capture == null) return;
            try { capture.Dispose(); } catch (Exception ex) { Storage.Log("Capture dispose: " + ex.Message); }
            capture = null;
        }
        private void CaptureLoop()
        {
            ScreenCapture capture = null;
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    EngineState current = state;
                    if (!current.Enabled || current.Editing || current.Settings.Mode != LightingMode.Screen)
                    {
                        CloseCapture(ref capture); captured = null;
                        if (stop.Token.WaitHandle.WaitOne(100)) break;
                        continue;
                    }
                    var captureClock = Stopwatch.StartNew();
                    DateTime captureStartedAt = DateTime.UtcNow;
                    try
                    {
                        if (capture == null) capture = new ScreenCapture();
                        Rgb[] colors = capture.Capture(current.Settings);
                        // A slow capture of an old layout must not replace the current one.
                        if (!stop.IsCancellationRequested && state.CaptureVersion == current.CaptureVersion)
                        {
                            captured = new CapturedFrame(colors, capture.Status, current.CaptureVersion, captureStartedAt);
                            outputWake.Set();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (state.CaptureVersion == current.CaptureVersion)
                            captured = new CapturedFrame(new Rgb[60], "Capture indisponible : " + ex.Message, current.CaptureVersion, captureStartedAt);
                        Storage.Log("Capture error: " + ex.Message); CloseCapture(ref capture);
                        if (stop.Token.WaitHandle.WaitOne(1000)) break;
                    }
                    if (stop.Token.WaitHandle.WaitOne(CapturePacing.RemainingMilliseconds(captureClock.ElapsedMilliseconds))) break;
                }
            }
            catch (Exception ex) { Storage.Log("Capture worker stopped: " + ex.Message); }
            finally { CloseCapture(ref capture); }
        }

        private void OutputLoop()
        {
            while (!stop.IsCancellationRequested)
            {
                if (FirmwareBusy) { status = "Mise à jour firmware — sortie suspendue"; if (stop.Token.WaitHandle.WaitOne(100)) break; continue; }
                bool ownsOutput = false;
                try
                {
                    while (!stop.IsCancellationRequested && !(ownsOutput = Monitor.TryEnter(OutputOwner, 100)))
                        status = "Attente de l'arrêt de l'ancienne connexion";
                    while (ownsOutput && !stop.IsCancellationRequested && !FirmwareBusy)
                    {
                        try { OutputTick(); }
                        catch (Exception ex)
                        {
                            status = "Connexion interrompue : " + ex.Message;
                            Storage.Log("Output error: " + ex.Message);
                            Disconnect(false); retryDelay = Stopwatch.StartNew();
                        }
                        int wait = lastWriteAt == null ? CapturePacing.FrameIntervalMilliseconds : CapturePacing.RemainingMilliseconds(lastWriteAt.ElapsedMilliseconds);
                        if (WaitHandle.WaitAny(new[] { stop.Token.WaitHandle, outputWake }, wait) == 0) break;
                    }
                }
                finally
                {
                    if (ownsOutput) { try { Disconnect(!keepOnAfterExit || FirmwareBusy); } finally { Monitor.Exit(OutputOwner); } }
                }
            }
        }

        private void OutputTick()
        {
            EngineState current = state;
            Settings s = current.Settings;
            if (startupBlackoutPending && !current.Enabled && !string.IsNullOrWhiteSpace(s.SerialPort))
            {
                if (serial != null) { Disconnect(true); startupBlackoutPending = false; status = "Éteint — contrôleur remis au noir"; return; }
                if (retryDelay != null && retryDelay.ElapsedMilliseconds < 3000) return;
                SendStartupBlackout(s.SerialPort); startupBlackoutPending = false;
                status = "Éteint — contrôleur remis au noir"; return;
            }
            DateTime now = DateTime.UtcNow;
            CapturedFrame image = captured;
            Rgb[] pixels = image != null && OutputPolicy.CanUseCapture(current.CaptureVersion, image.Version,
                (now - image.At).TotalSeconds) ? image.Colors : null;
            TestRequest request = test;
            bool testing = request != null && request.Active;
            bool identifying = testing && request.IdentifyLed > 0;
            if (testing && request.Effect.HasValue) s = TelemetryTestSettings.ForEffect(s, request.Effect.Value);
            bool enabled = current.Enabled && (!current.Editing || identifying);
            TelemetrySnapshot t = testing ? new TelemetrySnapshot(true, request.Left, request.Right,
                request.Yellow, request.Blue, request.Green, request.White, request.Black, request.Orange,
                request.Checkered, request.Abs, request.Tc, request.WheelLock, request.RpmPercent, now) : telemetry;
            if (testing) { t = t.WithTiming(request.Previous); request.Previous = t; }
            FrameResult frame = FrameComposer.Compose(s, enabled, pixels, t, current.Selection, now,
                identifying ? request.IdentifyLed : 0);
            lock (gate)
            {
                if (disposed || !object.ReferenceEquals(current, state)) return;
                lastFrame = frame;
            }
            if (!current.Enabled || s.PreviewOnly)
            {
                Disconnect(true);
                status = !current.Enabled ? "Éteint — aucun port ouvert" : "Aperçu uniquement — aucun envoi au matériel";
                return;
            }
            if (string.IsNullOrWhiteSpace(s.SerialPort))
            { Disconnect(true); status = "Envoi bloqué : sélectionner le port."; return; }
            if (serial != null && !string.Equals(serial.PortName, s.SerialPort, StringComparison.OrdinalIgnoreCase)) Disconnect(true);
            // Pause the lighting while editing, but keep an existing port instead of
            // repeatedly resetting the Arduino (and its uncontrolled startup RGB flashes).
            if (current.Editing && !identifying && serial == null)
            { status = "Placement des zones — sortie suspendue"; return; }
            if (serial == null)
            {
                if (retryDelay != null && retryDelay.ElapsedMilliseconds < 3000) return;
                Connect(s.SerialPort); return; // Recompose from current settings after startup.
            }
            byte[] packet = AdalightProtocol.Encode(frame.Colors);
            lock (gate)
            {
                // Changes of port/budget/OFF are serialized against the final Write call.
                // A packet already in the OS/USB queue cannot be retracted.
                if (disposed || !object.ReferenceEquals(current, state) || !current.Enabled || s.PreviewOnly) return;
                if (serial.BytesToWrite == 0 && (lastWriteAt == null || lastWriteAt.ElapsedMilliseconds >= CapturePacing.FrameIntervalMilliseconds))
                { serial.Write(packet, 0, packet.Length); lastWriteAt = Stopwatch.StartNew(); }
            }
            status = current.Editing && !identifying ? "Placement des zones — fond éteint, port conservé" :
                "Envoi Adalight — " + serial.PortName + " — 60 LED / 115200 bauds (sans accusé de réception)";
        }

        private bool ConnectionWanted(string portName)
        {
            EngineState current = state;
            return !stop.IsCancellationRequested && !FirmwareBusy && current.Enabled && !current.Settings.PreviewOnly &&
                string.Equals(current.Settings.SerialPort, portName, StringComparison.OrdinalIgnoreCase);
        }
        private bool WaitForController(string portName, int milliseconds)
        {
            var elapsed = Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < milliseconds)
            {
                if (!ConnectionWanted(portName)) return false;
                int wait = Math.Min(50, milliseconds - (int)elapsed.ElapsedMilliseconds);
                if (wait > 0 && stop.Token.WaitHandle.WaitOne(wait)) return false;
            }
            return ConnectionWanted(portName);
        }
        private void Connect(string portName)
        {
            if (!ConnectionWanted(portName)) return;
            status = "Ouverture de " + portName + " — attente du contrôleur";
            serial = new SerialPort(portName, AdalightProtocol.BaudRate, Parity.None, 8, StopBits.One);
            serial.Handshake = Handshake.None; serial.DtrEnable = false; serial.RtsEnable = false;
            serial.ReadTimeout = 100; serial.WriteTimeout = 250;
            serial.Open();
            if (!WaitForController(portName, 2200)) { Disconnect(true); return; }
            serial.DiscardInBuffer();
            // Resynchronization for the unchanged, blocking firmware. No ACK exists.
            byte[] zeros = new byte[AdalightProtocol.PacketLength];
            serial.Write(zeros, 0, zeros.Length);
            if (!WaitForController(portName, 34)) { Disconnect(true); return; }
            byte[] black = AdalightProtocol.Encode(new Rgb[60]);
            serial.Write(black, 0, black.Length);
            if (!WaitForController(portName, 34)) { Disconnect(true); return; }
            // The query contains no Adalight prefix; legacy receivers ignore it.
            byte[] query = { 69, 118, 111, 1, 0, 166 };
            serial.Write(query, 0, query.Length);
            if (!WaitForController(portName, 150)) { Disconnect(true); return; }
            evoFirmware = serial.ReadExisting().Contains("DIYAMBIENT-EVO/1\n");
            firmwareStatus = evoFirmware ? "Firmware EVO : extinction après 1 s sans trame" : "Firmware ancien / inconnu : extinction autonome non confirmée";
            retryDelay = null;
        }

        private void SendStartupBlackout(string portName)
        {
            serial = new SerialPort(portName, AdalightProtocol.BaudRate, Parity.None, 8, StopBits.One);
            serial.Handshake = Handshake.None; serial.DtrEnable = false; serial.RtsEnable = false;
            serial.ReadTimeout = 100; serial.WriteTimeout = 250; serial.Open();
            if (stop.Token.WaitHandle.WaitOne(2200)) { Disconnect(false); return; }
            serial.DiscardInBuffer();
            byte[] zeros = new byte[AdalightProtocol.PacketLength]; serial.Write(zeros, 0, zeros.Length);
            if (stop.Token.WaitHandle.WaitOne(34)) { Disconnect(false); return; }
            byte[] black = AdalightProtocol.Encode(new Rgb[60]); serial.Write(black, 0, black.Length);
            var elapsed = Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < 150 && serial.BytesToWrite > 0) Thread.Sleep(4);
            Disconnect(false); retryDelay = null;
        }

        private void Disconnect(bool black)
        {
            if (serial == null) return;
            try
            {
                if (!black && keepOnAfterExit && evoFirmware && serial.IsOpen)
                {
                    // Only a normal shutdown can explicitly suspend the watchdog.
                    byte[] hold = { 69, 118, 111, 2, 1, 164 };
                    serial.Write(hold, 0, hold.Length);
                    Thread.Sleep(34);
                }
                if (black && serial.IsOpen)
                {
                    byte[] packet = AdalightProtocol.Encode(new Rgb[60]);
                    serial.Write(packet, 0, packet.Length);
                    // Close can clear pending buffers. Give the final frame a bounded
                    // drain opportunity; USB transmission/LED extinction remain unconfirmed.
                    var elapsed = Stopwatch.StartNew();
                    while (elapsed.ElapsedMilliseconds < 150)
                    {
                        if (elapsed.ElapsedMilliseconds >= 34 && serial.BytesToWrite == 0) break;
                        Thread.Sleep(4);
                    }
                }
            }
            catch (Exception ex) { Storage.Log("Blackout not confirmed: " + ex.Message); }
            finally
            {
                try { serial.Dispose(); } catch (Exception ex) { Storage.Log("Serial dispose: " + ex.Message); }
                serial = null; evoFirmware = false; lastWriteAt = null;
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                keepOnAfterExit = state.Enabled && state.Settings.KeepOnAfterExit;
                state = new EngineState(state.Settings, false, false, state.CaptureVersion + 1);
                test = null; captured = null; telemetry = TelemetrySnapshot.Empty;
            }
            stop.Cancel();
            Task[] workers = { captureTask, outputTask };
            bool completed = false;
            try { completed = Task.WaitAll(workers, 300); }
            catch (AggregateException ex) { completed = captureTask.IsCompleted && outputTask.IsCompleted; Storage.Log("Worker shutdown: " + ex.GetBaseException().Message); }
            if (completed) { outputWake.Dispose(); stop.Dispose(); }
            else
            {
                Storage.Log("Stop pending: worker finishing; next serial owner will wait.");
                Task.Factory.ContinueWhenAll(workers, tasks =>
                {
                    foreach (Task task in tasks) if (task.IsFaulted) Storage.Log("Worker finish: " + task.Exception.GetBaseException().Message);
                    outputWake.Dispose(); stop.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            }
        }
    }
}
