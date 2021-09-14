using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Threading;
using System.Diagnostics;

namespace OverwolfInsiderPatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            string overwolfCorePath = "";

            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            if (isElevated)
            {
                Console.WriteLine("Role: Administrator");
            }
            else
            {
                Console.WriteLine("Role: User");
                Console.WriteLine("Please, run as administrator");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Process[] processes = Process.GetProcessesByName("overwolf");
            if (processes.Length > 0)
            {
                Console.WriteLine("Overwolf app is running, please close Overwolf before start this app");
                Console.ReadKey();
                Environment.Exit(0);
            }


            string winDir = Path.GetPathRoot(Environment.SystemDirectory);

            string[] directories = Directory.GetDirectories(winDir + "Program Files (x86)\\Overwolf");
            foreach (string dir in directories)
            {
                if (File.Exists(dir + "\\OverWolf.Client.Core.dll"))
                {
                    overwolfCorePath = dir + "\\OverWolf.Client.Core.dll";
                    Console.WriteLine("Overwolf.Client.Core.dll found!");
                }
            }

            Console.Write("Enter \"Overwolf.Client.Core.dll\" path");
            if (overwolfCorePath != "")
            {
                Console.Write(" (press enter to use default path)");
            }
            Console.Write(": ");


            string overwolfCoreNewPath = Console.Read().ToString();
            if (overwolfCoreNewPath != "" && File.Exists(overwolfCoreNewPath))
            {
                overwolfCorePath = overwolfCoreNewPath;
            }

            if (File.Exists(overwolfCorePath))
            {
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfCorePath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };
                AssemblyDefinition overwolfCore = AssemblyDefinition.ReadAssembly(overwolfCorePath, reader);
                TypeDefinition overwolfCoreWManager = overwolfCore.MainModule.GetType("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper");
                if (overwolfCoreWManager != null)
                {
                    Console.WriteLine("-- OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type found!");
                    MethodDefinition showInsiderBlockMessageMethod = null;
                    try
                    {
                        showInsiderBlockMessageMethod = overwolfCoreWManager.Methods.Single(x => x.Name == "ShowInsiderBlockMessage");
                    }
                    catch (InvalidOperationException e)
                    {
                        Console.WriteLine("ShowInsiderBlockMessage1 not found: " + e.Message);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: " + e);
                    }
                    if (showInsiderBlockMessageMethod != null)
                    {
                        Console.WriteLine("---- ShowInsiderBlockMessage method found!");
                        showInsiderBlockMessageMethod.Body.Instructions.Clear();
                        showInsiderBlockMessageMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        showInsiderBlockMessageMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        Console.WriteLine("------ ShowInsiderBlockMessage method patched!");

                        try
                        {
                            string backupFilePath = Path.GetDirectoryName(overwolfCorePath) + "\\" + Path.GetFileNameWithoutExtension(overwolfCorePath) + "_bak.dll";
                            if (File.Exists(backupFilePath))
                                File.Delete(backupFilePath);
                            File.Copy(overwolfCorePath, backupFilePath);
                            overwolfCore.Write(overwolfCorePath);
                            Console.WriteLine("-------- Patched successfully");
                        }
                        catch (System.UnauthorizedAccessException)
                        {
                            Console.WriteLine("Permission denied");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }

                }
                else
                {
                    Console.WriteLine("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type not found!");
                }
            }

            Console.ReadKey();
        }
    }
}
