using Microsoft.Win32;
using Bluscream;
using OverwolfPatcher.Classes;
using System;
using System.Collections.Generic;
using System.IO;

namespace OverwolfPatcher.Legacy
{
    // Retained for historical comparison only; never called by the entry point.
    internal static class LegacyWorkflow
    {
        static readonly List<IPatch> Patches = new List<IPatch>()
        { // Add new patches here
            new Patches.ClientCore(),
            new Patches.ClientBL(),
            new Patches.ClientCommonUtils(),
            new Patches.Subscriptions(),
            new Patches.Extensions()
        };
        static Overwolf ow;

        internal static void Run(string[] args)
        {
            Console.Title = $"{AssemblyInfo.Product} by {AssemblyInfo.Company} v{AssemblyInfo.Version}";

            Utils.RestartAsAdmin(args);

            ow = new Overwolf();

            if (ow.Processes.Count > 0)
            {
                Console.WriteLine("Overwolf app is running, do you want to close it now? (y/n)");
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y) ow.Processes.ForEach(p => { p.Kill(); p.WaitForExit(); });
                else Utils.ErrorAndExit("Cannot continue with Overwolf running!");
            }

            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(Overwolf.BaseRegKey);
            if (registryKey is null) Utils.ErrorAndExit("Can not find Overwolf registry keys, is Overwolf even installed?", reinstall_overwolf: true);

            ow.ProgramFolder = new DirectoryInfo(registryKey.GetValue("InstallFolder").ToString());

            registryKey = Registry.CurrentUser.OpenSubKey(Overwolf.MainRegKey);
            if (registryKey is null) Utils.ErrorAndExit("Can not find Overwolf registry keys, is Overwolf even installed?", reinstall_overwolf: true);

            ow.DataFolder = new DirectoryInfo(registryKey.GetValue("UserDataFolder").ToString());

            Console.WriteLine();

            foreach (var patch in Patches)
            {
                Exception error;
                var success = patch.TryPatch(ow, out error);
                // do whatever on success/error lol
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Complete!");
            Console.ResetColor();

            Console.ReadKey();

            // Overwolf.UrlProtocol.OpenInDefaultBrowser();
        }
    }
}
