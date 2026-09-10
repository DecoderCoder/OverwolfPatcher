using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Mono.Cecil;
using OverwolfPatcher.Classes;
using OverwolfPatcher.Testing;
using OverWolf.Client.Core.ODKv2.Profile;

namespace ODKv2API
{
    public enum SubscriptionState { Active = 3, Expired = 4 }
    public class DetailedActivePlan
    {
        public int PlanId { get; set; }
        public SubscriptionState State { get; set; }
        public long ExpiryDate { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public int PeriodMonths { get; set; }
    }
}
namespace OverWolf.Client.Core.ODKv2.Profile
{
    public class OverwolfSubscription
    {
        public string UID { get; set; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ODKv2API.DetailedActivePlan[] GetExtensionSubscriptions()
        {
            // Retain a real getter call in the release fixture, as in Overwolf.
            if (UID == "throw") throw new InvalidOperationException("original path");
            try { return new[] { new ODKv2API.DetailedActivePlan { PlanId = 7 } }; }
            finally { GC.KeepAlive(UID); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int[] GetExtensionSubscriptionsIds() => new[] { 7 };
    }
}
internal class Program
{
    const string App = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    const string CatalogApp = "cccccccccccccccccccccccccccccccccccccccc";
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception) { return; }
        throw new Exception(message);
    }
    static void RejectContaining(Action action, string expected, string message)
    {
        try { action(); }
        catch (Exception error)
        {
            if (error.Message.Contains(expected)) return;
            throw new Exception(message + " Wrong error: " + error.Message);
        }
        throw new Exception(message);
    }
    static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--compatibility") return CompatibilityProbe.Run(args);
            if (args.Length > 0) throw new ArgumentException("Unknown test option.");
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var original = Assembly.GetExecutingAssembly().Location;

            var extensionData = Path.Combine(directory, "overwolf-data");
            var extensionRoot = Path.Combine(extensionData, "Extensions");
            var secondApp = new string('p', 40);
            var firstVersion = Path.Combine(extensionRoot, App, "175.3.12981");
            var secondVersion = Path.Combine(extensionRoot, secondApp, "1.2.3.4");
            Directory.CreateDirectory(firstVersion);
            Directory.CreateDirectory(secondVersion);
            Directory.CreateDirectory(Path.Combine(extensionRoot, "not-an-extension"));
            File.WriteAllText(Path.Combine(firstVersion, "managed.dll"), "fixture");
            File.WriteAllText(Path.Combine(secondVersion, "native.EXE"), "fixture");
            var installed = new Overwolf { DataFolder = new DirectoryInfo(extensionData) };
            Assert(installed.GetInstalledExtensionIds().SequenceEqual(new[] { App, secondApp }.OrderBy(value => value)),
                "Installed extension discovery returned the wrong IDs");
            Assert(installed.GetInstalledExtensionBinaries().Select(file => file.Name)
                .SequenceEqual(new[] { "managed.dll", "native.EXE" }.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                "Installed extension binary discovery returned the wrong files");

            var unknownApp = new string('a', 40);
            var lookups = new ConcurrentBag<string>();
            var resolved = ExtensionPlanResolver.ResolveAll(new[] { App, CatalogApp, unknownApp },
                id => { lookups.Add(id); return id == App ? new[] { 101 } : id == CatalogApp ? new[] { 202, 203 } : new int[0]; });
            Assert(lookups.OrderBy(id => id).SequenceEqual(new[] { App, CatalogApp, unknownApp }.OrderBy(id => id)),
                "All-extension resolution did not query each extension independently");
            Assert(resolved.Selections.Length == 2, "Catalog plan resolution selected the wrong extensions");
            Assert(resolved.Selections.Single(selection => selection.ExtensionId == App).Plans.SequenceEqual(new[] { 101 }),
                "First extension's catalog plan was not retained");
            Assert(resolved.Selections.Single(selection => selection.ExtensionId == CatalogApp).Plans.SequenceEqual(new[] { 202, 203 }),
                "Second extension's catalog plans were not retained");
            Assert(resolved.Skipped.Length == 1 && resolved.Skipped[0].ExtensionId == unknownApp,
                "Unknown/no-plan extension was not skipped");
            var noCatalog = ExtensionPlanResolver.ResolveAll(new[] { App, CatalogApp }, id => new int[0]);
            Assert(noCatalog.Selections.Length == 0 && noCatalog.Skipped.Length == 2 &&
                noCatalog.Skipped.All(skip => skip.Reason.Contains("no plan metadata")),
                "Empty catalog results were not reported as non-discoverable legacy metadata");
            Assert(ExtensionPlanResolver.ParseCatalogPlanIds("[{\"id\":101},{\"id\":202},{\"id\":101}]")
                .SequenceEqual(new[] { 101, 202 }), "Catalog JSON plan IDs were not parsed or deduplicated");
            Reject(() => ExtensionPlanResolver.ParseCatalogPlanIds("{\"data\":[{\"id\":101}]}"),
                "An undocumented wrapped catalog response was accepted");
            Reject(() => ExtensionPlanResolver.ParseCatalogPlanIds("[{\"planId\":101}]"),
                "A non-catalog planId field was accepted");
            Reject(() => ExtensionPlanResolver.ParseCatalogPlanIds("[{\"id\":101.5}]"),
                "A fractional catalog plan ID was accepted");
            Reject(() => ExtensionPlanResolver.ParseCatalogPlanIds("[{\"id\":\"101\"}]"),
                "A string catalog plan ID was accepted");
            var invalidCatalog = ExtensionPlanResolver.ResolveAll(new[] { App }, id => new[] { -1 });
            Assert(invalidCatalog.Selections.Length == 0 && invalidCatalog.Skipped.Length == 1,
                "An invalid catalog plan set was selected");
            Reject(() => ExtensionPlanResolver.ResolveSingle(unknownApp, id => new int[0]),
                "Single extension with no catalog plans was accepted");
            RejectContaining(() => PremiumCommand.Run(new[] { "instrument", "--mode", "premium", "--plans", "101" }),
                "--plans is not accepted", "In-memory instrumentation accepted a caller-supplied plan ID");

            byte[] patched;
            using (var assembly = AssemblyDefinition.ReadAssembly(original))
            {
                PremiumAssembly.Rewrite(assembly, App, new[] { 61, 62 });
                Reject(() => PremiumAssembly.Rewrite(assembly, App, new[] { 61 }), "Repatch was accepted");
                using (var stream = new MemoryStream()) { assembly.Write(stream); patched = stream.ToArray(); }
            }
            // Execute rewritten methods in a synthetic fixture, never installed Overwolf code.
            var loaded = Assembly.Load(patched);
            var type = loaded.GetType("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription");
            var instance = Activator.CreateInstance(type);
            type.GetProperty("UID").SetValue(instance, App);
            var result = (Array)type.GetMethod("GetExtensionSubscriptions").Invoke(instance, null);
            Assert(result.Length == 2, "Wrong number of plans");
            var plan = result.GetValue(0);
            Assert((int)plan.GetType().GetProperty("PlanId").GetValue(plan) == 61, "Plan not injected");
            Assert(Convert.ToInt32(plan.GetType().GetProperty("State").GetValue(plan)) == 3, "Enum value was hardcoded");
            Assert((double)plan.GetType().GetProperty("Price").GetValue(plan) == 0d, "Double setter failed");
            Assert((long)plan.GetType().GetProperty("ExpiryDate").GetValue(plan) > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "Expired fixture");
            Assert(((int[])type.GetMethod("GetExtensionSubscriptionsIds").Invoke(instance, null)).SequenceEqual(new[] { 61, 62 }), "ID API mismatch");
            // Advance fixture data into the past without altering the machine clock.
            // The generated methods have no runtime expiry gate: seven days is data,
            // not a promise that the local fixture automatically stops working.
            using (var stream = new MemoryStream(patched))
            using (var expired = AssemblyDefinition.ReadAssembly(stream))
            {
                var detailed = expired.MainModule.GetType("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription")
                    .Methods.Single(m => m.Name == "GetExtensionSubscriptions");
                foreach (var instruction in detailed.Body.Instructions.Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldc_I8))
                    instruction.Operand = 1L;
                using (var output = new MemoryStream())
                {
                    expired.Write(output);
                    var expiredType = Assembly.Load(output.ToArray()).GetType(type.FullName);
                    var expiredInstance = Activator.CreateInstance(expiredType);
                    expiredType.GetProperty("UID").SetValue(expiredInstance, App);
                    var expiredPlans = (Array)expiredType.GetMethod("GetExtensionSubscriptions").Invoke(expiredInstance, null);
                    var expiredPlan = expiredPlans.GetValue(0);
                    Assert((long)expiredPlan.GetType().GetProperty("ExpiryDate").GetValue(expiredPlan) == 1L, "Past expiry fixture not exercised");
                    Assert(Convert.ToInt32(expiredPlan.GetType().GetProperty("State").GetValue(expiredPlan)) == 3, "Past expiry unexpectedly changed generated state");
                    Assert(((int[])expiredType.GetMethod("GetExtensionSubscriptionsIds").Invoke(expiredInstance, null)).Contains(61), "Generated IDs unexpectedly enforce expiry");
                }
            }
            type.GetProperty("UID").SetValue(instance, "another-app");
            result = (Array)type.GetMethod("GetExtensionSubscriptions").Invoke(instance, null);
            Assert((int)result.GetValue(0).GetType().GetProperty("PlanId").GetValue(result.GetValue(0)) == 7, "Other app changed");
            Assert(((int[])type.GetMethod("GetExtensionSubscriptionsIds").Invoke(instance, null)).Single() == 7, "Other app IDs changed");
            type.GetProperty("UID").SetValue(instance, "throw");
            Reject(() => type.GetMethod("GetExtensionSubscriptions").Invoke(instance, null), "Original exception behavior lost");
            using (var assembly = AssemblyDefinition.ReadAssembly(original))
            {
                assembly.Name.PublicKey = new byte[] { 1 };
                Reject(() => PremiumAssembly.Rewrite(assembly, App, new[] { 61 }), "Signed assembly accepted");
            }
            using (var assembly = AssemblyDefinition.ReadAssembly(original))
            {
                var body = assembly.MainModule.GetType("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription").Methods.Single(m => m.Name == "GetExtensionSubscriptions").Body;
                var count = body.Instructions.Count;
                assembly.MainModule.GetType("ODKv2API.DetailedActivePlan").Methods.Single(m => m.Name == "set_Price").Parameters[0].ParameterType = assembly.MainModule.TypeSystem.Single;
                Reject(() => PremiumAssembly.Rewrite(assembly, App, new[] { 61 }), "Changed signature accepted");
                Assert(body.Instructions.Count == count, "Failed preflight mutated method");
            }
            var version = Path.Combine(directory, "0.1.2.3");
            Directory.CreateDirectory(version);
            var target = Path.Combine(version, PremiumAssembly.FileName);
            var saved = Path.Combine(directory, "original.dll");
            var staged = Path.Combine(directory, "patched.dll");
            File.Copy(original, target);
            File.Copy(original, saved);
            File.WriteAllBytes(staged, patched);
            var hash = PremiumCommand.Hash(target);
            new XDocument(new XElement("PremiumTest", new XAttribute("version", "0.1.2.3"),
                new XAttribute("original", hash), new XAttribute("patched", PremiumCommand.Hash(staged))))
                .Save(Path.Combine(directory, "manifest.xml"));
            Reject(() => PremiumCommand.ReplaceChecked(target, staged, "wrong-hash"), "Changed target accepted");
            Assert(PremiumCommand.Hash(target) == hash, "Rejected apply changed target");
            PremiumCommand.ReplaceChecked(target, staged, hash);
            PremiumCommand.Restore(target, directory);
            PremiumCommand.Restore(target, directory);
            Assert(PremiumCommand.Hash(target) == hash && File.Exists(saved), "Restore failed or consumed backup");
            File.Delete(target);
            PremiumCommand.Restore(target, directory);
            Assert(PremiumCommand.Hash(target) == hash, "Missing DLL restoration failed");
            File.AppendAllText(target, "simulated update");
            Reject(() => PremiumCommand.Restore(target, directory), "Restore overwrote changed installation");

            Console.WriteLine("PASS: scoped IL execution, original paths, signature guards, repeated patch rejection, atomic replacement, and restoration.");
            Console.WriteLine("PASS: past expiry remains fixture data; unknown Overwolf versions use metadata discovery.");
            Console.WriteLine("PASS: startup profiler launch gates are kept separate from offline rewrite tests.");
            Console.WriteLine("Test artifacts: " + directory);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

}
