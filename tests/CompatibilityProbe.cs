using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Mono.Cecil;
using OverwolfPatcher.Testing;

// Opt-in, read-only evidence test for the inspected installation. Never loads Core
// or the launcher for execution and never invokes FileUtils (which initializes logging).
internal static class CompatibilityProbe
{
    const string CommonHash = "33298BC101F699732EE6A52CB7BE7A02FBC97F97AC4217C61F1801BB1A335F43";
    const string CoreHash = "9DA15E0CACF446E59B3F728BA78F5CC8F0EC6D6616BB0F490BF90ABD21EF098E";

    internal static int Run(string[] args)
    {
        if (args.Length != 5)
            throw new ArgumentException("Usage: OverwolfPatcher.Tests --compatibility INSTALL CLEAN_COPY STAGED_COPY ROUNDTRIP_COPY");
        var install = Path.GetFullPath(args[1]);
        var common = Path.Combine(install, "0.309.0.14", "OverWolf.Client.CommonUtils.dll");
        var core = Path.Combine(install, "0.309.0.14", PremiumAssembly.FileName);
        var launcher = Path.Combine(install, "Overwolf.exe");
        var config = Path.Combine(install, "Overwolf.exe.config");
        var copies = args.Skip(2).Select(Path.GetFullPath).ToArray();
        var paths = new[] { common, core, launcher, config }.Concat(copies).Distinct().ToArray();
        var hashes = paths.ToDictionary(p => p, PremiumCommand.Hash);
        var versions = XDocument.Load(config).Descendants().Where(e => e.Name.LocalName == "probing")
            .SelectMany(e => ((string)e.Attribute("privatePath") ?? "").Split(';'))
            .Where(p => Version.TryParse(p, out _) && p.Split('.').Length == 4).Distinct().ToArray();
        Require(versions.SequenceEqual(new[] { "0.309.0.14" }), "Active installation version is not the reviewed baseline.");
        // Pin the only proprietary assembly executed by this process to the reviewed
        // clean version. New versions need a fresh static inspection, not automatic use.
        Require(hashes[common] == CommonHash, "Unreviewed verifier binary; refusing execution.");
        Require(hashes[core] == CoreHash && hashes[copies[0]] == CoreHash, "Clean baseline changed.");
        Console.WriteLine("Read-only compatibility probe; UTC " + DateTime.UtcNow.ToString("o"));
        foreach (var path in paths) Console.WriteLine(hashes[path] + "  " + path);

        try
        {
            CheckLifecycle(launcher);
            var loaded = Assembly.LoadFrom(common);
            var type = loaded.GetType("OverWolf.Client.CommonUtils.Utils.Security.WinTrust", true);
            var method = type.GetMethod("VerifyEmbeddedSignature", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string), typeof(bool).MakeByRefType() }, null);
            Require(method != null, "Reviewed signature helper is missing.");
            for (int i = 0; i < copies.Length; i++)
            {
                var parameters = new object[] { copies[i], false };
                var accepted = (bool)method.Invoke(null, parameters);
                Console.WriteLine(Path.GetFileName(copies[i]) + ": embedded signature accepted=" + accepted + ", broken=" + parameters[1]);
                Require(accepted == (i == 0), "Signature result differs from the clean/unsigned hypothesis.");
            }
            CompareBodies(copies[0], copies[1], true);
            CompareBodies(copies[0], copies[2], false);
            Console.WriteLine("PASS: embedded signature rejection and recursive method-body scope checks.");
            Console.WriteLine("This tests a necessary launcher predicate, not the full FileSignedByOverwolf policy, CLR strong-name validation, startup, or premium behavior.");
            return 0;
        }
        finally
        {
            foreach (var path in paths) Require(PremiumCommand.Hash(path) == hashes[path], "Input changed during the probe: " + path);
            Console.WriteLine("All input hashes unchanged.");
        }
    }

    static void CheckLifecycle(string path)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(path))
        {
            var program = assembly.MainModule.GetType("OverWolf.Client.Launcher.Program");
            Require(program != null, "Launcher Program missing.");
            foreach (var name in new[] { "VerifyDeployedAssembliesOrFail", "AssemblyResolveByVersion", "OnAssemblyLoad" })
            {
                var method = program.Methods.Single(m => m.Name == name);
                Require(Calls(method, "VerifyDeployedAssembly"), name + " changed its verifier call path.");
                Console.WriteLine("Observed verifier call site: " + method.FullName);
            }
            var main = assembly.EntryPoint;
            Require(main.DeclaringType == program && Calls(main, "RegisterAssemblyLoadGuard") && Calls(main, "VerifyDeployedAssembliesOrFail"), "Entry lifecycle changed.");
            Require(Calls(program.Methods.Single(m => m.Name == "OnAssemblyLoad"), "FailFast"), "Load-time failure path changed.");
            Require(Calls(program.Methods.Single(m => m.Name == "VerifyDeployedAssembly"), "FileSignedByOverwolf"), "Publisher verifier changed.");
        }
    }

    static bool Calls(MethodDefinition method, string name) => method.HasBody && method.Body.Instructions
        .Select(i => i.Operand).OfType<MethodReference>().Any(m => m.Name == name);

    static IEnumerable<TypeDefinition> Types(TypeDefinition type) => new[] { type }.Concat(type.NestedTypes.SelectMany(Types));

    static void CompareBodies(string original, string candidate, bool patched)
    {
        using (var left = AssemblyDefinition.ReadAssembly(original))
        using (var right = AssemblyDefinition.ReadAssembly(candidate))
        {
            // Cecil FullName alone is not unique (static/instance and generic
            // overloads can share it). Include declaration order and verify it too.
            var before = left.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods)
                .Select((m, i) => new { Key = i + "|" + m.FullName, Method = m }).ToDictionary(m => m.Key, m => Body(m.Method));
            var after = right.MainModule.Types.SelectMany(Types).SelectMany(t => t.Methods)
                .Select((m, i) => new { Key = i + "|" + m.FullName, Method = m }).ToDictionary(m => m.Key, m => Body(m.Method));
            Require(before.Keys.OrderBy(k => k).SequenceEqual(after.Keys.OrderBy(k => k)), "Method inventory changed.");
            var changed = before.Keys.Where(k => before[k] != after[k]).Select(k => k.Substring(k.IndexOf('|') + 1)).OrderBy(k => k).ToArray();
            Console.WriteLine(Path.GetFileName(candidate) + ": " + before.Count + " methods including nested types; " + changed.Length + " body/attribute differences.");
            foreach (var name in changed) Console.WriteLine("  " + name);
            var expected = new[] {
                "ODKv2API.DetailedActivePlan[] OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription::GetExtensionSubscriptions()",
                "System.Int32[] OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription::GetExtensionSubscriptionsIds()"
            }.OrderBy(k => k);
            Require(patched ? changed.SequenceEqual(expected) : changed.Length == 0, "Unexpected method scope difference.");
        }
    }

    // Compare IL, local types, EH boundaries, and method flags. MaxStack is excluded
    // because Cecil can recalculate it. This is not a proof of
    // semantic equivalence: resources, other metadata, PE layout and signatures differ.
    static string Body(MethodDefinition method)
    {
        var prefix = method.Attributes + "|" + method.ImplAttributes + "|";
        if (!method.HasBody) return prefix;
        var body = method.Body;
        return prefix + body.InitLocals + "|" + string.Join(";", body.Variables.Select(v => v.VariableType.FullName)) + "|" +
            string.Join(";", body.Instructions.Select(i => i.ToString())) + "|" + string.Join(";", body.ExceptionHandlers.Select(h =>
                h.HandlerType + ":" + h.CatchType?.FullName + ":" + h.TryStart?.Offset + ":" + h.TryEnd?.Offset + ":" +
                h.HandlerStart?.Offset + ":" + h.HandlerEnd?.Offset + ":" + h.FilterStart?.Offset));
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
