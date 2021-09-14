using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;

namespace OverwolfInsiderPatcher
{
    class Program
    {
        static void Main()
        {
            Console.Title = "Overwolf patcher by Decode 1.1";
            
            string overwolfCorePath = "";
            string overwolfCoreCUPath = "";

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
            Console.WriteLine();
            string[] directories = Directory.GetDirectories(winDir + "Program Files (x86)\\Overwolf");
            foreach (string dir in directories)
            {
                if (File.Exists(dir + "\\OverWolf.Client.Core.dll"))
                {
                    overwolfCorePath = dir + "\\OverWolf.Client.Core.dll";
                    Console.WriteLine("Overwolf.Client.Core.dll found!");
                }
                if (File.Exists(dir + "\\OverWolf.Client.CommonUtils.dll"))
                {
                    overwolfCoreCUPath = dir + "\\OverWolf.Client.CommonUtils.dll";
                    Console.WriteLine("OverWolf.Client.CommonUtils.dll found!");
                }
            }
            //Console.Write("Enter \"Overwolf.Client.Core.dll\" path");
            //if (overwolfCorePath != "")
            //{
            //    Console.Write(" (press enter to use default path)");
            //}
            //Console.Write(": ");


            //string overwolfCoreNewPath = Console.ReadLine().ToString();
            //if (overwolfCoreNewPath != "" && File.Exists(overwolfCoreNewPath))
            //{
            //    overwolfCorePath = overwolfCoreNewPath;
            //}

            //Console.Write("Enter \"OverWolf.Client.CommonUtils.dll\" path");
            //if (overwolfCorePath != "")
            //{
            //    Console.Write(" (press enter to use default path)");
            //}
            //Console.Write(": ");

            //string overwolfCoreCUNewPath = Console.ReadLine().ToString();
            //if (overwolfCoreCUNewPath != "" && File.Exists(overwolfCoreCUNewPath))
            //{
            //    overwolfCoreCUPath = overwolfCoreCUNewPath;
            //}

            //Console.WriteLine(overwolfCoreCUNewPath);
            if (File.Exists(overwolfCorePath))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                         OverWolf.Client.Core.dll                      ||");
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfCorePath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };
                AssemblyDefinition overwolfCore = AssemblyDefinition.ReadAssembly(overwolfCorePath, reader);
                TypeDefinition overwolfCoreWManager = overwolfCore.MainModule.GetType("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper");
                if (overwolfCoreWManager != null)
                {
                    Console.WriteLine("|| OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type found! ||");
                    MethodDefinition showInsiderBlockMessageMethod = overwolfCoreWManager.Methods.SingleOrDefault(x => x.Name == "ShowInsiderBlockMessage");
                    if (showInsiderBlockMessageMethod != null)
                    {
                        Console.WriteLine("|| -- ShowInsiderBlockMessage method found!                              ||");
                        showInsiderBlockMessageMethod.Body.Instructions.Clear();
                        showInsiderBlockMessageMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        showInsiderBlockMessageMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        Console.WriteLine("|| ---- ShowInsiderBlockMessage method patched!                          ||");

                        TypeDefinition overwolfCoreIU = overwolfCore.MainModule.GetType("OverWolf.Client.Core.ODKv2.OverwolfInternalUtils");
                        if (overwolfCoreIU != null)
                        {
                            MethodDefinition overwolfCoreGPI = overwolfCoreIU.Methods.SingleOrDefault(x => x.Name == "getProductInformation");
                            if (overwolfCoreGPI != null)
                            {
                                foreach (Instruction instr in overwolfCoreGPI.Body.Instructions)
                                {
                                    if (instr.Operand != null && instr.Operand.GetType() == typeof(string) && ((string)instr.Operand).StartsWith("Copyright Overwolf © ") && !((string)instr.Operand).EndsWith(" (Patched by Decode)"))
                                    {
                                        instr.Operand = instr.Operand.ToString() + " (Patched by Decode)";
                                    }
                                }
                            }
                        }

                        try
                        {
                            string backupFilePath = Path.GetDirectoryName(overwolfCorePath) + "\\" + Path.GetFileNameWithoutExtension(overwolfCorePath) + "_bak.dll";
                            if (File.Exists(backupFilePath))
                                File.Delete(backupFilePath);
                            File.Copy(overwolfCorePath, backupFilePath);
                            overwolfCore.Write(overwolfCorePath);
                            Console.WriteLine("|| ------ Patched successfully                                           ||");
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
                    else
                    {
                        Console.WriteLine("|| ShowInsiderBlockMessage not found!                                    ||");
                    }

                }
                else
                {
                    Console.WriteLine("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type not found!");
                }
                Console.WriteLine("|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }

            if (File.Exists(overwolfCoreCUPath))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                      OverWolf.Client.CommonUtils.dll                   ||");
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfCoreCUPath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };
                AssemblyDefinition overwolfCore = AssemblyDefinition.ReadAssembly(overwolfCoreCUPath, reader);
                TypeDefinition overwolfCoreCUFeatures = overwolfCore.MainModule.GetType("OverWolf.Client.CommonUtils.Features.CommonFeatures");
                if (overwolfCoreCUFeatures != null)
                {
                    Console.WriteLine("|| OverWolf.Client.CommonUtils.Features.CommonFeatures type found!        ||");
                    MethodDefinition enableDevToolsForQA = overwolfCoreCUFeatures.Methods.SingleOrDefault(x => x.Name == "EnableDevToolsForQA");
                    if (enableDevToolsForQA != null)
                    {
                        Console.WriteLine("|| -- EnableDevToolsForQA method found!                                   ||");
                        enableDevToolsForQA.Body.Variables.Clear();
                        enableDevToolsForQA.Body.Instructions.Clear();
                        enableDevToolsForQA.Body.ExceptionHandlers.Clear();
                        enableDevToolsForQA.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        enableDevToolsForQA.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        Console.WriteLine("|| ---- EnableDevToolsForQA method patched!                               ||");

                        try
                        {
                            string backupFilePath = Path.GetDirectoryName(overwolfCoreCUPath) + "\\" + Path.GetFileNameWithoutExtension(overwolfCoreCUPath) + "_bak.dll";
                            if (File.Exists(backupFilePath))
                                File.Delete(backupFilePath);
                            File.Copy(overwolfCoreCUPath, backupFilePath);
                            overwolfCore.Write(overwolfCoreCUPath);
                            Console.WriteLine("|| ------ Patched successfully                                            ||");
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
                    else
                    {
                        Console.WriteLine("EnableDevToolsForQA not found!");
                    }

                }
                else
                {
                    Console.WriteLine("OverWolf.Client.CommonUtils.Features.CommonFeatures type not found!");
                }
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }


            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Complete!");

            Console.ReadKey();
        }
    }
}
