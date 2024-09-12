using Microsoft.Win32;
using System;
using System.IO;
using System.Collections.Generic;
using Bluscream;
using OverwolfPatcher.Classes;

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


        static void Main(string[] args)
        {
            Console.Title = $"{AssemblyInfo.Product} by {AssemblyInfo.Company} v{AssemblyInfo.Version}";

            Utils.RestartAsAdmin(args);

            ow = new Overwolf();

            if (ow.Processes.Count > 0)
            {
                Console.WriteLine("Overwolf app is running, do you want to close it now? (y/n)");
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Y) ow.Processes.ForEach(p => p.Kill());
                else {
                    Console.WriteLine("Cannot continue, press any key to exit");
                    Console.ReadKey();
                    Utils.Exit(1);
                }
            }

            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Overwolf");
            ow.ProgramFolder = new DirectoryInfo(registryKey.GetValue("InstallFolder").ToString());

            registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Overwolf\Overwolf");
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

            Console.WriteLine("Complete!");

            Console.ReadKey();
        }
    }
}
