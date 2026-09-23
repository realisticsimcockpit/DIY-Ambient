using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DIYAmbient.Plugin
{
    [DataContract]
    internal sealed class GitHubFirmwareFile
    {
        [DataMember] public string content { get; set; }
        [DataMember] public string encoding { get; set; }
        [DataMember] public string sha { get; set; }
        [DataMember] public int size { get; set; }
    }
    internal sealed class FirmwareSource
    {
        internal readonly string Text, Sha;
        internal FirmwareSource(string text, string sha) { Text = text; Sha = sha; }
    }
    // Maintenance only. Normal lighting never launches a process or downloads files.
    internal static class FirmwareFlasher
    {
        internal static readonly string[] Boards = { "Arduino Uno — ATmega328P", "Arduino Nano — ATmega328P", "Arduino Nano — ATmega328P (ancien bootloader)" };
        private static readonly string[] Targets = { "arduino:avr:uno", "arduino:avr:nano:cpu=atmega328", "arduino:avr:nano:cpu=atmega328old" };
        internal static readonly string Folder = Path.Combine(Storage.Folder, "FirmwareTools");
        private static readonly string Cli = Path.Combine(Folder, "arduino-cli.exe");
        private static readonly object MaintenanceGate = new object();
        internal static FirmwareSource DownloadGitHub(Action<string> report)
        {
            FirmwareSource source = null;
            Exclusive(() =>
            {
                Directory.CreateDirectory(Folder);
                report("Récupération du firmware depuis realisticsimcockpit/DIY-Ambient (main)…");
                string json;
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "DIY-Ambient-EVO";
                    client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    json = client.DownloadString("https://api.github.com/repos/realisticsimcockpit/DIY-Ambient/contents/firmware/Adalight_WS2812/Adalight_WS2812.ino?ref=main");
                }
                GitHubFirmwareFile file;
                using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    file = (GitHubFirmwareFile)new DataContractJsonSerializer(typeof(GitHubFirmwareFile)).ReadObject(input);
                if (file == null || file.encoding != "base64" || file.size < 500 || file.size > 16000)
                    throw new InvalidDataException("Fichier firmware GitHub inattendu.");
                byte[] bytes = Convert.FromBase64String(file.content);
                if (bytes.Length != file.size) throw new InvalidDataException("Taille firmware incorrecte.");
                byte[] header = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
                byte[] blob = new byte[header.Length + bytes.Length];
                Buffer.BlockCopy(header, 0, blob, 0, header.Length); Buffer.BlockCopy(bytes, 0, blob, header.Length, bytes.Length);
                string sha;
                using (var hash = SHA1.Create()) sha = BitConverter.ToString(hash.ComputeHash(blob)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(sha, file.sha, StringComparison.Ordinal)) throw new InvalidDataException("Empreinte GitHub incorrecte.");
                string text = new UTF8Encoding(false, true).GetString(bytes);
                if (!text.Contains("#define NUM_LEDS 60") || !text.Contains("#define DATA_PIN 6") || !text.Contains("#define serialRate 115200"))
                    throw new InvalidDataException("Le firmware GitHub ne correspond pas à 60 LED / D6 / 115200.");
                source = new FirmwareSource(text, sha);
                report("Firmware GitHub chargé — " + sha.Substring(0, 12) + ". Aucun flash effectué.");
            });
            return source;
        }

        private static void Exclusive(Action action)
        {
            if (!Monitor.TryEnter(MaintenanceGate)) throw new InvalidOperationException("Maintenance firmware déjà en cours.");
            try { action(); } finally { Monitor.Exit(MaintenanceGate); }
        }

        internal static void Prepare(Action<string> report)
        {
            Exclusive(() =>
            {
                Directory.CreateDirectory(Folder);
                string avr = Path.Combine(Folder, "data", "packages", "arduino", "hardware", "avr", "1.8.6");
                string fastLed = Path.Combine(Folder, "user", "libraries", "FastLED", "library.properties");
                if (File.Exists(Cli) && File.Exists(Path.Combine(avr, "boards.txt")) &&
                    File.Exists(Path.Combine(avr, "platform.txt")) && File.Exists(fastLed) &&
                    File.Exists(Path.Combine(Folder, "user", "libraries", "FastLED", "src", "FastLED.h")) &&
                    Regex.IsMatch(File.ReadAllText(fastLed), @"(?m)^version=3\.9\.15\s*$"))
                { report("Outils Arduino et FastLED déjà prêts."); return; }
                if (!File.Exists(Cli))
                {
                    report("Téléchargement Arduino CLI 1.5.1 depuis arduino.cc…");
                    string zip = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".zip");
                    try
                    {
                        using (var client = new WebClient())
                            client.DownloadFile("https://downloads.arduino.cc/arduino-cli/arduino-cli_1.5.1_Windows_64bit.zip", zip);
                        using (var archive = ZipFile.OpenRead(zip))
                        {
                            var entry = archive.GetEntry("arduino-cli.exe");
                            if (entry == null) throw new InvalidDataException("Archive Arduino CLI inattendue.");
                            // Extract only the expected executable, never arbitrary archive paths.
                            entry.ExtractToFile(Cli, false);
                        }
                    }
                    finally { if (File.Exists(zip)) File.Delete(zip); }
                }
                Run("core update-index", 300000, report);
                Run("core install arduino:avr@1.8.6", 600000, report);
                Run("lib update-index", 300000, report);
                Run("lib install FastLED@3.9.15", 600000, report);
                report("Outils prêts : Arduino AVR 1.8.6 / FastLED 3.9.15. Aucun flash effectué.");
            });
        }

        private static string Compile(int board, Action<string> report, FirmwareSource source)
        {
            if (board < 0 || board >= Targets.Length) throw new ArgumentException("Choisir le modèle et le bootloader exacts. Une carte WAVGAT ne suffit pas à les identifier.");
            string job = Path.Combine(Folder, "jobs", Guid.NewGuid().ToString("N"));
            string sketch = Path.Combine(job, "Adalight_WS2812"), output = Path.Combine(job, "build");
            Directory.CreateDirectory(sketch); Directory.CreateDirectory(output);
            foreach (string name in new[] { "Adalight_WS2812.ino" })
            {
                if (source != null)
                {
                    File.WriteAllText(Path.Combine(sketch, name), source.Text, new UTF8Encoding(false));
                    continue;
                }
                using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("DIYAmbient.Firmware." + name))
                {
                    if (input == null) throw new InvalidDataException("Source firmware absente du plugin : " + name);
                    using (var file = File.Create(Path.Combine(sketch, name))) input.CopyTo(file);
                }
            }
            report("Compilation FastLED — 60 LED WS2812, DATA D6…");
            Run("compile --fqbn " + Targets[board] + " --output-dir " + Quote(output) + " " + Quote(sketch), 300000, report);
            return output;
        }

        internal static void CompileOnly(int board, Action<string> report, FirmwareSource source)
        { Exclusive(() => { string output = Compile(board, report, source); report("Compilation réussie — aucun flash. Fichiers : " + output); }); }

        internal static void Flash(int board, string port, Action<string> report, FirmwareSource source)
        {
            Exclusive(() =>
            {
                if (port == null || !Regex.IsMatch(port, @"\ACOM[1-9][0-9]{0,3}\z", RegexOptions.IgnoreCase))
                    throw new ArgumentException("Port COM invalide.");
                // Build must succeed before the serial output is interrupted.
                if (source == null) throw new InvalidOperationException("Charger le firmware depuis GitHub avant de flasher.");
                string output = Compile(board, report, source);
                report("Libération de " + port + "… Ne pas débrancher la carte pendant le flash.");
                AmbientEngine.WithFirmwarePort(() =>
                {
                    Run("upload --fqbn " + Targets[board] + " --port " + port + " --input-dir " + Quote(output) + " --verify", 180000, report);
                });
                report("Téléversement et vérification mémoire réussis. Validation des LED sur matériel encore nécessaire.");
            });
        }

        private static string Quote(string value) { return "\"" + value + "\""; }
        private static void Run(string arguments, int timeoutMs, Action<string> report)
        {
            if (!File.Exists(Cli)) throw new FileNotFoundException("Préparer les outils Arduino avant de compiler.", Cli);
            RunProcess(Cli, arguments, timeoutMs, report, true);
        }
        private static string RunProcess(string executable, string arguments, int timeoutMs, Action<string> report, bool logOutput)
        {
            var info = new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Folder };
            info.EnvironmentVariables["ARDUINO_DIRECTORIES_DATA"] = Path.Combine(Folder, "data");
            info.EnvironmentVariables["ARDUINO_DIRECTORIES_DOWNLOADS"] = Path.Combine(Folder, "downloads");
            info.EnvironmentVariables["ARDUINO_DIRECTORIES_USER"] = Path.Combine(Folder, "user");
            var log = new StringBuilder(); var logGate = new object();
            DataReceivedEventHandler receive = (sender, e) =>
            {
                if (e.Data == null) return;
                lock (logGate) { log.AppendLine(e.Data); if (log.Length > 32000) log.Remove(0, log.Length - 32000); }
            };
            using (var process = new Process { StartInfo = info })
            {
                process.OutputDataReceived += receive; process.ErrorDataReceived += receive;
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                if (!process.WaitForExit(timeoutMs))
                {
                    // Never release the COM lease while a child uploader may still run.
                    report("Délai dépassé : attente de la fin de l'outil Arduino. Port réservé, ne pas fermer SimHub.");
                    process.WaitForExit();
                }
                process.WaitForExit(); // Finish asynchronous output drains.
                string result; lock (logGate) result = log.ToString();
                if (logOutput) Storage.Log("Firmware / " + arguments + Environment.NewLine + result);
                if (process.ExitCode != 0) throw new InvalidOperationException("Arduino a échoué (" + process.ExitCode + ").\n" + result);
                return result;
            }
        }
    }
}
