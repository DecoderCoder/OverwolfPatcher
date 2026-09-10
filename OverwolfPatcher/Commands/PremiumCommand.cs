using System;
// Command entry point retained in the OverwolfPatcher.Testing namespace for compatibility.
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;
using Mono.Cecil;
using OverwolfPatcher.Classes;

namespace OverwolfPatcher.Testing
{
    internal static class PremiumCommand
    {
        const string ProfilerClsid = "{4D7C38E9-7C8A-4F5D-9D2C-1D3E7BC9F1A4}";
        internal static int Run(string[] args)
        {
            var noArguments = args.Length == 0;
            var command = noArguments ? "instrument" : args[0].ToLowerInvariant();
            if (command == "--help" || command == "help")
            {
                Console.WriteLine("OverwolfPatcher status|stage|apply|restore|baseline|instrument [--install DIR] [--output DIR] [--backup DIR]");
                Console.WriteLine("No arguments launches instrument --mode premium --all-extensions with catalog-resolved per-extension plans.");
                Console.WriteLine("The legacy on-disk status/stage/apply commands require --app EXTENSION_ID --plans 1,2.");
                Console.WriteLine("status is read-only. stage writes a copy. apply/restore require Overwolf to be closed.");
                Console.WriteLine("baseline launches OverwolfLauncher.exe without profiling; use --entry managed to reproduce direct Overwolf.exe.");
                Console.WriteLine("instrument launches OverwolfLauncher.exe so profiling reaches only its managed Overwolf.exe child; --mode bootstrap|observe|flags|neutral|premium (default neutral).");
                Console.WriteLine("instrument options: --mode MODE --profiler PROFILER_X64_DLL --log LOG_FILE --wait-ms N [--verbose]");
                Console.WriteLine("instrument premium options: --app EXTENSION_ID or --all-extensions [--data DIR]. Plan IDs are resolved automatically.");
                Console.WriteLine("Local legacy subscription API testing only; login is required. No server subscription is granted.");
                return 0;
            }
            if (!new[] { "status", "stage", "apply", "restore", "baseline", "instrument" }.Contains(command)) throw new ArgumentException("Unknown command. Use --help.");
            var options = new Dictionary<string, string>();
            if (noArguments)
            {
                options.Add("--all-extensions", "true");
                options.Add("--mode", "premium");
                options.Add("--verbose", "true");
            }
            var flags = new[] { "--all-extensions", "--verbose" };
            for (int i = 1; i < args.Length; i++)
            {
                if (flags.Contains(args[i]))
                {
                    if (options.ContainsKey(args[i])) throw new ArgumentException("Invalid or duplicate option: " + args[i]);
                    options.Add(args[i], "true");
                    continue;
                }
                if (i + 1 == args.Length || !new[] { "--install", "--output", "--backup", "--app", "--plans", "--mode", "--profiler", "--log", "--wait-ms", "--entry", "--data" }.Contains(args[i]) || options.ContainsKey(args[i]))
                    throw new ArgumentException("Invalid or duplicate option: " + args[i]);
                options.Add(args[i], args[i + 1]);
                i++;
            }
            if (command == "instrument" && options.ContainsKey("--plans"))
                throw new ArgumentException("--plans is not accepted by in-memory instrumentation; premium plan IDs are resolved automatically from Overwolf's per-extension catalog.");
            var install = Path.GetFullPath(Get(options, "--install", DiscoverInstall()));
            var version = ActiveVersion(install);
            var target = Path.Combine(install, version, PremiumAssembly.FileName);
            if (command == "baseline") return Baseline(install, version, options);
            if (command == "instrument") return Instrument(install, version, target, options);
            if (command == "restore")
            {
                RequireStopped();
                Restore(target, Get(options, "--backup", null) ?? throw new ArgumentException("restore requires --backup DIR"));
                return 0;
            }
            var allExtensions = options.ContainsKey("--all-extensions");
            if (allExtensions && command != "instrument")
                throw new ArgumentException("--all-extensions is supported only with instrument --mode premium.");
            var app = Get(options, "--app", null)
                ?? throw new ArgumentException(command + " requires --app EXTENSION_ID.");
            if (allExtensions && options.ContainsKey("--app"))
                throw new ArgumentException("Use either --app or --all-extensions, not both.");
            if (app.Length != 40 || app.Any(c => c < 'a' || c > 'p')) throw new ArgumentException("Expected a 40-character Overwolf extension ID.");
            var planText = Get(options, "--plans", null)
                ?? throw new ArgumentException("Specify --plans for this app.");
            var plans = planText.Split(',').Select(int.Parse).Distinct().ToArray();
            if (plans.Length == 0 || plans.Length > 32 || plans.Any(p => p <= 0)) throw new ArgumentException("Specify 1 to 32 positive plan IDs.");
            Console.WriteLine("Overwolf " + version + " | " + target);
            Console.WriteLine("App: " + app + " | local plans: " + string.Join(",", plans));
            var originalHash = Hash(target);
            using (var resolver = new DefaultAssemblyResolver())
            {
                resolver.AddSearchDirectory(Path.GetDirectoryName(target));
                using (var assembly = AssemblyDefinition.ReadAssembly(target, new ReaderParameters { AssemblyResolver = resolver, InMemory = true }))
                {
                    if (assembly.MainModule.Resources.Any(r => r.Name == PremiumAssembly.Marker))
                    {
                        Console.WriteLine("This DLL already contains a local premium test. Restore it before applying again.");
                        return command == "status" ? 0 : 2;
                    }
                    PremiumAssembly.Rewrite(assembly, app, plans);
                    Console.WriteLine("Legacy API structure is compatible. Other apps retain the original methods.");
                    if (command == "status") return 0;
                    if (command == "apply") RequireStopped();
                    var output = Path.GetFullPath(Get(options, "--output", Path.Combine(Environment.CurrentDirectory,
                        "artifacts", "premium-tests", version, Guid.NewGuid().ToString("N"))));
                    if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                        throw new IOException("Output directory must be empty to preserve previous tests/backups.");
                    Directory.CreateDirectory(output);
                    var staged = Path.Combine(output, PremiumAssembly.FileName);
                    assembly.Write(staged);
                    using (var check = AssemblyDefinition.ReadAssembly(staged))
                        if (!check.MainModule.Resources.Any(r => r.Name == PremiumAssembly.Marker)) throw new IOException("Staged assembly validation failed.");
                    File.Copy(target, Path.Combine(output, "original.dll"), false);
                    if (Hash(target) != originalHash || Hash(Path.Combine(output, "original.dll")) != originalHash)
                        throw new IOException("Overwolf files changed while staging. Nothing was applied; retry after the update finishes.");
                    new XDocument(new XElement("PremiumTest", new XAttribute("version", version), new XAttribute("app", app),
                        new XAttribute("plans", string.Join(",", plans)), new XAttribute("original", originalHash),
                        new XAttribute("patched", Hash(staged)), new XAttribute("createdUtc", DateTime.UtcNow.ToString("o"))))
                        .Save(Path.Combine(output, "manifest.xml"));
                    Console.WriteLine("Staged DLL and immutable original: " + output);
                    if (command == "stage")
                    {
                        Console.WriteLine("Installation unchanged. Staging does not prove that the app accepts a local plan.");
                        return 0;
                    }
                    RequireStopped();
                    ReplaceChecked(target, staged, originalHash);
                    Console.WriteLine("Applied local API test. Sign in to Overwolf, then open the app to verify it.");
                    Console.WriteLine("Restore: OverwolfPatcher restore --backup \"" + output + "\"");
                    return 0;
                }
            }
        }

        internal static void Restore(string target, string backup)
        {
            var manifest = XDocument.Load(Path.Combine(backup, "manifest.xml")).Root;
            if (manifest == null || manifest.Name != "PremiumTest") throw new IOException("Invalid backup manifest.");
            if (new DirectoryInfo(Path.GetDirectoryName(target)).Name != (string)manifest.Attribute("version"))
                throw new IOException("Backup belongs to a different Overwolf version.");
            var original = Path.Combine(backup, "original.dll");
            var expected = (string)manifest.Attribute("original");
            if (Hash(original) != expected) throw new IOException("Original backup hash does not match its manifest.");
            if (!File.Exists(target))
            {
                File.Copy(original, target, false);
                Console.WriteLine("Restored missing DLL from the original backup.");
                return;
            }
            var current = Hash(target);
            if (current == expected) { Console.WriteLine("Already restored."); return; }
            if (current != (string)manifest.Attribute("patched"))
                throw new IOException("DLL changed since this test (possibly an update). Refusing to overwrite it with an old backup.");
            ReplaceChecked(target, original, current);
            Console.WriteLine("Restored original DLL. Backup retained.");
        }

        internal static void ReplaceChecked(string target, string source, string expectedHash)
        {
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary, false);
                if (Hash(target) != expectedHash) throw new IOException("Target changed before replacement; operation cancelled.");
                File.Replace(temporary, target, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        static string ActiveVersion(string install)
        {
            var config = XDocument.Load(Path.Combine(install, "Overwolf.exe.config"));
            var paths = config.Descendants().Where(e => e.Name.LocalName == "probing")
                .SelectMany(e => ((string)e.Attribute("privatePath") ?? "").Split(';'))
                .Select(p => p.Trim())
                .Where(p => p.Length > 0 && File.Exists(Path.Combine(install, p, PremiumAssembly.FileName)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length != 1) throw new IOException("Cannot uniquely identify the active Core assembly directory from the launcher configuration.");
            return paths[0];
        }

        static string DiscoverInstall()
        {
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = machine.OpenSubKey(@"SOFTWARE\Overwolf"))
                return key?.GetValue("InstallFolder") as string ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Overwolf");
        }

        static string DiscoverDataFolder()
        {
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            using (var key = user.OpenSubKey(Overwolf.MainRegKey))
                return key?.GetValue("UserDataFolder") as string ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Overwolf");
        }

        static string[] DiscoverInstalledExtensionIds(Dictionary<string, string> options)
        {
            var dataFolder = Path.GetFullPath(Get(options, "--data", DiscoverDataFolder()));
            var overwolf = new Overwolf { DataFolder = new DirectoryInfo(dataFolder) };
            return overwolf.GetInstalledExtensionIds().ToArray();
        }

        static void RequireStopped()
        {
            var own = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcesses())
                using (process)
                    if (process.Id != own && process.ProcessName.StartsWith("Overwolf", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Close Overwolf and its apps before applying/restoring. Running: " + process.ProcessName);
        }

        static int Instrument(string install, string version, string target, Dictionary<string, string> options)
        {
            RequireStopped();
            if (!File.Exists(target)) throw new FileNotFoundException("The managed Core assembly was not found.", target);

            var executable = Path.Combine(install, "Overwolf.exe");
            var launcher = Path.Combine(install, "OverwolfLauncher.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("The managed Overwolf executable was not found.", executable);
            if (!File.Exists(launcher)) throw new FileNotFoundException("The native Overwolf launcher was not found.", launcher);
            if (!IsPe64(executable) || !IsPe64(launcher)) throw new NotSupportedException("Overwolf entry executables are not x64 PE files.");
            try { AssemblyName.GetAssemblyName(executable); }
            catch (Exception error) { throw new NotSupportedException("Overwolf.exe is not the managed entry executable.", error); }

            var profiler = Path.GetFullPath(Get(options, "--profiler",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverwolfPatcher.Profiler.x64.dll")));
            if (!File.Exists(profiler)) throw new FileNotFoundException("The x64 CLR profiler was not found. Build the native profiler first.", profiler);
            var mode = Get(options, "--mode", "neutral").ToLowerInvariant();
            if (mode != "bootstrap" && mode != "observe" && mode != "flags" && mode != "neutral" && mode != "premium")
                throw new ArgumentException("--mode must be bootstrap, observe, flags, neutral, or premium.");

            var selectedApp = Get(options, "--app", null);
            var allExtensions = options.ContainsKey("--all-extensions") ||
                (mode == "premium" && selectedApp == null);
            var verbose = options.ContainsKey("--verbose") || allExtensions;
            if (allExtensions && mode != "premium")
                throw new ArgumentException("--all-extensions requires --mode premium.");
            if (allExtensions && options.ContainsKey("--app"))
                throw new ArgumentException("Use either --app or --all-extensions, not both.");

            if (selectedApp != null && (selectedApp.Length != 40 || selectedApp.Any(c => c < 'a' || c > 'p')))
                throw new ArgumentException("Expected a 40-character Overwolf extension ID.");
            var apps = mode != "premium"
                ? new string[0]
                : allExtensions ? DiscoverInstalledExtensionIds(options) : new[] { selectedApp };
            if (mode == "premium" && apps.Length == 0)
                throw new IOException("No installed Overwolf extensions were found.");
            if (apps.Length > 256)
                throw new ArgumentException("At most 256 installed extensions can be selected for one instrumented session.");
            ExtensionPlanResolution planResolution = null;
            if (mode == "premium")
            {
                planResolution = allExtensions
                    ? ExtensionPlanResolver.ResolveAll(apps)
                    : new ExtensionPlanResolution(new[] {
                        ExtensionPlanResolver.ResolveSingle(selectedApp)
                    }, new ExtensionPlanSkip[0]);
                if (planResolution.Selections.Length == 0)
                {
                    var details = string.Join(Environment.NewLine, planResolution.Skipped.Select(skip =>
                        "  " + skip.ExtensionId + ": " + skip.Reason));
                    throw new ArgumentException("No installed extension had authoritative legacy plan metadata, so no premium patch was applied. Unsupported extensions were left unchanged." +
                        (string.IsNullOrWhiteSpace(details) ? string.Empty : Environment.NewLine + details));
                }
            }
            var waitText = Get(options, "--wait-ms", "0");
            if (!int.TryParse(waitText, out var waitMs) || waitMs < 0 || waitMs > 60000)
                throw new ArgumentException("--wait-ms must be between 0 and 60000.");

            var log = Path.GetFullPath(Get(options, "--log", Path.Combine(Environment.CurrentDirectory,
                "artifacts", "profiler", version + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log")));
            var logDirectory = Path.GetDirectoryName(log);
            if (!string.IsNullOrWhiteSpace(logDirectory)) Directory.CreateDirectory(logDirectory);

            if (verbose)
            {
                PrintSection("OVERWOLF PATCHER");
                PrintKeyValue("Mode", mode.ToUpperInvariant() + " (in-memory)");
                PrintKeyValue("Overwolf version", version);
                PrintKeyValue("Target binary", target);
                PrintKeyValue("Profiler", profiler);
                PrintKeyValue("Plan mapping", mode == "premium"
                    ? string.Join(";", planResolution.Selections.Select(selection => selection.ExtensionId + "=" + string.Join(",", selection.Plans)))
                    : "original methods");
                PrintKeyValue("Patch scope", mode != "premium" ? "metadata-identified Core methods" :
                    allExtensions ? "catalog-supported installed extensions" : "selected extension");
                PrintStatus("OK", "Overwolf processes are stopped and launcher/Core path checks passed.", ConsoleColor.Green);

                PrintSection("EXTENSION PATCH ATTEMPTS");
                for (var index = 0; index < apps.Length; index++)
                {
                    var selection = mode == "premium"
                        ? planResolution.Selections.SingleOrDefault(candidate => candidate.ExtensionId == apps[index])
                        : null;
                    if (selection == null)
                    {
                        var skipped = mode == "premium"
                            ? planResolution.Skipped.SingleOrDefault(candidate => candidate.ExtensionId == apps[index])
                            : null;
                        var reason = skipped == null
                            ? "not selected for premium mapping"
                            : skipped.Reason;
                        PrintStatus("SKIP", string.Format("[{0:00}/{1:00}] {2} -> {3}", index + 1, apps.Length, apps[index], reason), ConsoleColor.Yellow);
                    }
                    else
                        PrintStatus("TRY", string.Format("[{0:00}/{1:00}] {2} -> local plans {3} ({4})", index + 1, apps.Length, apps[index], string.Join(",", selection.Plans), selection.Source), ConsoleColor.Cyan);
                }
                if (mode == "premium" && planResolution.Skipped.Length > 0)
                    Console.WriteLine("  Unmapped extensions are intentionally not patched.");
                Console.WriteLine();
                Console.WriteLine("  Each mapped row enables an extension-ID guard with its own plan list in the shared Core method.");
                Console.WriteLine("  Runtime SetIL results appear below when --wait-ms is supplied and in the profiler log.");
            }

            // Some hosted shells expose both Path and PATH. .NET Framework's
            // ProcessStartInfo turns the inherited block into a case-insensitive
            // dictionary and throws when those aliases coexist.
            var inheritedPath = Environment.GetEnvironmentVariable("Path") ?? Environment.GetEnvironmentVariable("PATH");
            if (inheritedPath != null)
            {
                Environment.SetEnvironmentVariable("PATH", null);
                Environment.SetEnvironmentVariable("Path", inheritedPath);
            }
            var start = new ProcessStartInfo
            {
                FileName = launcher,
                Arguments = "-from-desktop",
                WorkingDirectory = install,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            start.EnvironmentVariables["COR_ENABLE_PROFILING"] = "1";
            start.EnvironmentVariables["COR_PROFILER"] = ProfilerClsid;
            start.EnvironmentVariables["COR_PROFILER_PATH"] = profiler;
            start.EnvironmentVariables["COR_PROFILER_PATH_64"] = profiler;
            start.EnvironmentVariables["COMPLUS_ProfAPI_ProfilerCompatibilitySetting"] = "EnableV2Profiler";
            start.EnvironmentVariables["OVERWOLF_PATCHER_TARGET_PROCESS"] = executable;
            start.EnvironmentVariables["OVERWOLF_PATCHER_PROFILER_LOG_PER_PROCESS"] = "1";
            start.EnvironmentVariables["OVERWOLF_PATCHER_PROFILER_MODE"] = mode;
            start.EnvironmentVariables["OVERWOLF_PATCHER_PROFILER_LOG"] = log;
            start.EnvironmentVariables.Remove("OVERWOLF_PATCHER_APP");
            start.EnvironmentVariables.Remove("OVERWOLF_PATCHER_APPS");
            start.EnvironmentVariables.Remove("OVERWOLF_PATCHER_PLANS");
            start.EnvironmentVariables.Remove("OVERWOLF_PATCHER_PREMIUM_MAP");
            start.EnvironmentVariables.Remove("OVERWOLF_PATCHER_PROFILER_VERBOSE");
            if (verbose) start.EnvironmentVariables["OVERWOLF_PATCHER_PROFILER_VERBOSE"] = "1";
            if (mode == "premium")
            {
                start.EnvironmentVariables["OVERWOLF_PATCHER_PREMIUM_MAP"] = planResolution.ToEnvironmentValue();
            }
            var launchedPid = 0;
            using (var process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("Overwolf could not be started.");
                launchedPid = process.Id;
                if (verbose)
                    PrintStatus("OK", "Launched native OverwolfLauncher PID " + process.Id + "; profiler targets managed Overwolf.exe only.", ConsoleColor.Green);
                else
                    Console.WriteLine("Launched native OverwolfLauncher PID " + process.Id + "; profiler targets managed Overwolf.exe only.");
                if (waitMs > 0) CaptureWindows(process.Id, log, waitMs);
            }
            if (verbose)
            {
                PrintSection("PATCH RESULTS");
                PrintKeyValue("Mode", mode);
                PrintKeyValue("Profiler log", log);
                if (waitMs > 0) PrintProfilerLogSummary(log);
                else
                    PrintStatus("INFO", "Runtime results are still asynchronous; rerun with --wait-ms 5000 to display the log summary.", ConsoleColor.Yellow);
            }
            else
                Console.WriteLine("Mode: " + mode + " | profiler log: " + log);
            if (waitMs > 0) Console.WriteLine("Window diagnostic capture: " + WindowLogPath(log, launchedPid));
            if (verbose)
                PrintStatus("OK", "Installed files were not modified. The patch ends when the instrumented Overwolf session exits.", ConsoleColor.Green);
            else
                Console.WriteLine("Installed files were not modified. Remove the profiling environment by launching Overwolf normally.");
            return 0;
        }

        static int Baseline(string install, string version, Dictionary<string, string> options)
        {
            RequireStopped();
            var managedExecutable = Path.Combine(install, "Overwolf.exe");
            var nativeLauncher = Path.Combine(install, "OverwolfLauncher.exe");
            var entry = Get(options, "--entry", "launcher").ToLowerInvariant();
            if (entry != "launcher" && entry != "managed") throw new ArgumentException("--entry must be launcher or managed.");
            var executable = entry == "launcher" ? nativeLauncher : managedExecutable;
            if (!File.Exists(executable)) throw new FileNotFoundException("The selected Overwolf executable was not found.", executable);
            if (!IsPe64(executable)) throw new NotSupportedException("The selected Overwolf executable is not an x64 PE.");
            if (!File.Exists(managedExecutable)) throw new FileNotFoundException("The managed Overwolf executable was not found.", managedExecutable);
            try { AssemblyName.GetAssemblyName(managedExecutable); }
            catch (Exception error) { throw new NotSupportedException("Overwolf.exe is not the managed entry executable.", error); }

            var waitText = Get(options, "--wait-ms", "0");
            if (!int.TryParse(waitText, out var waitMs) || waitMs < 0 || waitMs > 60000)
                throw new ArgumentException("--wait-ms must be between 0 and 60000.");
            var log = Path.GetFullPath(Get(options, "--log", Path.Combine(Environment.CurrentDirectory,
                "artifacts", "profiler", version + "-baseline-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log")));
            var logDirectory = Path.GetDirectoryName(log);
            if (!string.IsNullOrWhiteSpace(logDirectory)) Directory.CreateDirectory(logDirectory);
            var inheritedPath = Environment.GetEnvironmentVariable("Path") ?? Environment.GetEnvironmentVariable("PATH");
            if (inheritedPath != null)
            {
                Environment.SetEnvironmentVariable("PATH", null);
                Environment.SetEnvironmentVariable("Path", inheritedPath);
            }
            var start = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = entry == "launcher" ? "-from-desktop" : "",
                WorkingDirectory = install,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            foreach (var name in new[] { "COR_ENABLE_PROFILING", "COR_PROFILER", "COR_PROFILER_PATH",
                "COR_PROFILER_PATH_32", "COR_PROFILER_PATH_64", "COMPLUS_ProfAPI_ProfilerCompatibilitySetting" })
                start.EnvironmentVariables.Remove(name);

            var launchedPid = 0;
            using (var process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("Overwolf could not be started.");
                launchedPid = process.Id;
                Console.WriteLine("Launched baseline " + entry + " Overwolf PID " + process.Id + " without CLR profiling.");
                if (waitMs > 0) CaptureWindows(process.Id, log, waitMs);
            }
            if (waitMs > 0) Console.WriteLine("Baseline window diagnostic capture: " + WindowLogPath(log, launchedPid));
            Console.WriteLine("No installation or profiling environment was modified.");
            return 0;
        }

        static void CaptureWindows(int processId, string log, int waitMs)
        {
            try { Thread.Sleep(waitMs); }
            catch (ThreadInterruptedException) { return; }

            var pids = new HashSet<int> { processId };
            foreach (var candidate in Process.GetProcesses())
            {
                using (candidate)
                    if (candidate.ProcessName.StartsWith("Overwolf", StringComparison.OrdinalIgnoreCase)) pids.Add(candidate.Id);
            }
            var lines = new List<string> { "rootPid=" + processId + " observedPids=" + string.Join(",", pids.OrderBy(value => value)) };
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (!pids.Contains((int)owner)) return true;
                AddWindow(lines, "top", window);
                EnumChildWindows(window, (child, __) => { AddWindow(lines, "child", child); return true; }, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
            var path = WindowLogPath(log, processId);
            File.WriteAllLines(path, lines.Count == 0
                ? new[] { "No windows found for PID " + processId + " after " + waitMs + " ms." }
                : lines, Encoding.UTF8);
            Console.WriteLine("Captured " + lines.Count + " window records for PID " + processId + ": " + path);
        }

        static void AddWindow(List<string> lines, string kind, IntPtr handle)
        {
            var title = WindowText(handle);
            var className = new StringBuilder(256);
            GetClassName(handle, className, className.Capacity);
            lines.Add(kind + " hwnd=" + handle + " visible=" + IsWindowVisible(handle) +
                " class=" + className + " title=" + title);
        }

        static string WindowText(IntPtr handle)
        {
            var length = GetWindowTextLength(handle);
            var value = new StringBuilder(Math.Max(1, length + 1));
            GetWindowText(handle, value, value.Capacity);
            return value.ToString().Replace("\r", "\\r").Replace("\n", "\\n");
        }

        static string WindowLogPath(string log, int processId)
        {
            var directory = Path.GetDirectoryName(log) ?? Environment.CurrentDirectory;
            var name = Path.GetFileNameWithoutExtension(log) + "." + processId + ".windows.txt";
            return Path.Combine(directory, name);
        }

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
        [DllImport("user32.dll")]
        static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr state);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr window, StringBuilder className, int capacity);
        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr window);

        delegate bool EnumWindowsProc(IntPtr window, IntPtr state);

        static bool IsPe64(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                if (stream.Length < 0x40) return false;
                stream.Position = 0x3C;
                var peOffset = reader.ReadInt32();
                if (peOffset < 0 || peOffset > stream.Length - 6) return false;
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550) return false;
                return reader.ReadUInt16() == 0x8664;
            }
        }

        static void PrintSection(string title)
        {
            const int width = 70;
            title = (title ?? string.Empty).Trim();
            if (title.Length > width - 2) title = title.Substring(0, width - 2);
            Console.WriteLine();
            Console.WriteLine("+" + new string('-', width) + "+");
            Console.WriteLine("| " + title.PadRight(width - 2) + " |");
            Console.WriteLine("+" + new string('-', width) + "+");
        }

        static void PrintKeyValue(string name, string value)
        {
            Console.WriteLine("  " + (name ?? string.Empty).PadRight(18) + ": " + (value ?? string.Empty));
        }

        static void PrintStatus(string status, string message, ConsoleColor color)
        {
            var previous = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine("  [" + (status ?? string.Empty).PadRight(4) + "] " + message);
            }
            finally { Console.ForegroundColor = previous; }
        }

        static void PrintProfilerLogSummary(string log)
        {
            var files = GetProfilerLogFiles(log).ToArray();
            if (files.Length == 0)
            {
                PrintStatus("WARN", "No profiler log has been written yet.", ConsoleColor.Yellow);
                return;
            }

            var printed = 0;
            foreach (var file in files)
            {
                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch (IOException error)
                {
                    PrintStatus("WARN", "Could not read " + file + ": " + error.Message, ConsoleColor.Yellow);
                    continue;
                }

                foreach (var line in lines.Where(IsPatchLogLine))
                {
                    var succeeded = line.IndexOf("SetILFunctionBody detailed=ok ids=ok", StringComparison.OrdinalIgnoreCase) >= 0;
                    var failed = line.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("refusing", StringComparison.OrdinalIgnoreCase) >= 0;
                    PrintStatus(succeeded ? "OK" : failed ? "FAIL" : "LOG",
                        Path.GetFileName(file) + ": " + line, succeeded ? ConsoleColor.Green : failed ? ConsoleColor.Red : ConsoleColor.Gray);
                    printed++;
                }
            }
            if (printed == 0)
                PrintStatus("INFO", "Profiler started, but no patch result has been logged during the wait period.", ConsoleColor.Yellow);
        }

        static bool IsPatchLogLine(string line)
        {
            return line.IndexOf("premium extension", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("profiler initialized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("SetILFunctionBody", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Core module observed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("refusing", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static IEnumerable<string> GetProfilerLogFiles(string log)
        {
            var directory = Path.GetDirectoryName(log) ?? Environment.CurrentDirectory;
            var baseName = Path.GetFileName(log);
            if (File.Exists(log)) yield return log;
            if (!Directory.Exists(directory)) yield break;

            var stem = Path.GetFileNameWithoutExtension(log);
            var extension = Path.GetExtension(log);
            foreach (var file in Directory.GetFiles(directory)
                .Where(path => !string.Equals(Path.GetFileName(path), baseName, StringComparison.OrdinalIgnoreCase) &&
                               Path.GetFileName(path).StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                yield return file;
        }

        static string Get(Dictionary<string, string> options, string name, string fallback) => options.TryGetValue(name, out var value) ? value : fallback;
    }
}
