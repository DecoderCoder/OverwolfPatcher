using Bluscream;
using Microsoft.Win32;
using OverwolfPatcher.Classes;
using System;
using System.Collections.Generic;
using System.IO;

namespace OverwolfPatcher
{
    class Program
    {
        static List<IPatch> Patches = new List<IPatch>()
        { // Add new patches here
            new Patches.ClientCore(),
            new Patches.ClientBL(),
            new Patches.ClientCommonUtils(),
            new Patches.Subscriptions(),
            new Patches.Extensions()
        };
        static Overwolf ow;


        static int Main(string[] args)
        {
            try { return Testing.PremiumCommand.Run(args); }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAILED: " + error.Message);
                return 1;
            }
        }

        // Retained for historical comparison only; never called by the entry point.
        static void LegacyMain(string[] args)
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
