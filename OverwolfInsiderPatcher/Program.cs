using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace OverwolfInsiderPatcher
{
    class Program
    {
        static string Log(string text, int length = 73)
        {
            text = "|| " + text;
            for (int i = 0; text.Length < length; i++)
            {
                text += " ";
            }
            text = text + " ||";
            Console.WriteLine(text);
            return text;
        }

        static List<string> removeFromToRow(string from, string where, string to, string insert = "")
        {
            List<string> list;
            if (where.Contains("\r\n"))
                list = where.Split(new[] { "\r\n" }, StringSplitOptions.None).ToList();
            else
                list = where.Split(new[] { "\n" }, StringSplitOptions.None).ToList();
            return removeFromToRow(from, list, to, insert);
        }

        static List<string> removeFromToRow(string from, List<string> where, string to, string insert = "")
        {
            int start = -1;
            int end = -1;
            for (int i = 0; i < where.Count; i++)
            {
                if (where[i] == from)
                {
                    start = i;
                }
                if (start != -1 && where[i] == to)
                {
                    end = i;
                    break;
                }
            }

            if (start != -1 && end != -1)
            {
                where.RemoveRange(start, end - start + 1);
            }
            if (insert != "")
            {
                where.Insert(start, insert);
            }

            return where;
        }

        static void Main()
        {
            Console.Title = "Overwolf patcher by Decode 1.33";

            string overwolfPath = "";
            string overwolfDataPath = "";
            string overwolfExtensionsPath = "";
            string overwolfCorePath = "";
            string overwolfCoreCUPath = "";
            string overwolfSubscriptionsPath = "";
            string overwolfExtensionsDllPath = ""; // Overwolf.Extensions.dll
            string overwolfBDDllPath = ""; // Overwolf.Extensions.dll

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

            RegistryKey registryKey = Registry.LocalMachine;
            registryKey = registryKey.OpenSubKey(@"SOFTWARE\WOW6432Node\Overwolf");
            overwolfPath = registryKey.GetValue("InstallFolder").ToString();

            registryKey = Registry.CurrentUser;
            registryKey = registryKey.OpenSubKey(@"Software\Overwolf\Overwolf");
            overwolfDataPath = registryKey.GetValue("UserDataFolder").ToString();
            overwolfExtensionsPath = overwolfDataPath + @"\Extensions\";


            Console.WriteLine();
            string[] directories = Directory.GetDirectories(overwolfPath);
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
                if (File.Exists(dir + "\\Overwolf.Extensions.dll"))
                {
                    overwolfExtensionsDllPath = dir + "\\Overwolf.Extensions.dll";
                    Console.WriteLine("Overwolf.Extensions.dll found!");
                }
                if (File.Exists(dir + "\\Overwolf.Subscriptions.dll"))
                {
                    overwolfSubscriptionsPath = dir + "\\Overwolf.Subscriptions.dll";
                    Console.WriteLine("Overwolf.Subscriptions.dll found!");
                }             
                if (File.Exists(dir + "\\OverWolf.Client.BL.dll"))
                {
                    overwolfBDDllPath = dir + "\\OverWolf.Client.BL.dll";
                    Console.WriteLine("OverWolf.Client.BL.dll found!");
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
                bool successful = true;
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                         OverWolf.Client.Core.dll                       ||");
                Console.WriteLine("||                                                                        ||");
                var resolver = new DefaultAssemblyResolver();                
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfCorePath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, InMemory = true };
                AssemblyDefinition overwolfCore = AssemblyDefinition.ReadAssembly(overwolfCorePath, reader);
                TypeDefinition overwolfCoreWManager = overwolfCore.MainModule.GetType("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper");
                if (overwolfCoreWManager != null)
                {
                    Console.WriteLine("|| OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type found!  ||");
                    MethodDefinition showInsiderBlockMessageMethod = overwolfCoreWManager.Methods.SingleOrDefault(x => x.Name == "ShowInsiderBlockMessage");
                    if (showInsiderBlockMessageMethod != null)
                    {
                        Console.WriteLine("|| -- ShowInsiderBlockMessage method found!                               ||");
                        showInsiderBlockMessageMethod.Body.Instructions.Clear();
                        showInsiderBlockMessageMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        showInsiderBlockMessageMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        Console.WriteLine("|| ---- ShowInsiderBlockMessage method patched!                           ||");

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
                    }
                    else
                    {
                        Console.WriteLine("|| ShowInsiderBlockMessage not found!                                    ||");
                    }

                    TypeDefinition overwolfCoreProfile = overwolfCore.MainModule.GetType("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription");
                    if (overwolfCoreProfile != null)
                    {
                        Console.WriteLine("|| OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription type found!    ||");
                        MethodDefinition overwolfCoreGES = overwolfCoreProfile.Methods.SingleOrDefault(x => x.Name == "GetExtensionSubscriptions");
                        if (overwolfCoreGES != null)
                        {
                            Console.WriteLine("|| -- GetExtensionSubscriptions method found!                             ||");
                            try
                            {
                                overwolfCoreGES = InjectMethods.OverwolfCoreGetExtensionSubscriptions(ref overwolfCore, overwolfCoreGES);
                            } catch(Exception e)
                            {
                                successful = false;
                                Console.WriteLine("Error, Overwolf.Core will not be patched: ");
                                Console.WriteLine(e);
                            }
                        }
                            
                        //
                    }

                }
                else
                {
                    Console.WriteLine("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type not found!");
                }


                TypeDefinition overwolfCoreExtensionWebApp = overwolfCore.MainModule.GetType("OverWolf.Client.Core.ODKv2.ExtensionWebApp"); // you think this will already work? i can try yes
                {
                    Console.WriteLine("|| OverWolf.Client.Core.ODKv2.ExtensionWebApp type found!    ||");
                    MethodDefinition overwolfCoreExtensionWebAppStratContentValidation = overwolfCoreExtensionWebApp.Methods.SingleOrDefault(x => x.Name == "StratContentValidation");
                    if (overwolfCoreExtensionWebAppStratContentValidation != null)
                    {
                        Console.WriteLine("|| -- StratContentValidation method found!                             ||");
                        try
                        {
                            overwolfCoreExtensionWebAppStratContentValidation.Body.Instructions.Clear();
                            overwolfCoreExtensionWebAppStratContentValidation.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        } catch (Exception e)
                        {
                            successful = false;
                            Console.WriteLine("Error, Overwolf.Core will not be patched: ");
                            Console.WriteLine(e);
                        }
                    }

                    //
                }


                string backupFilePath = Path.GetDirectoryName(overwolfCorePath) + "\\" + Path.GetFileNameWithoutExtension(overwolfCorePath) + "_bak.dll";




                try
                {
                    if (File.Exists(backupFilePath))
                        File.Delete(backupFilePath);
                    File.Copy(overwolfCorePath, backupFilePath);
                    if(successful)
                    overwolfCore.Write(overwolfCorePath);
                    Console.WriteLine("|| ------ Patched successfully                                            ||");
                }
                catch (System.UnauthorizedAccessException)
                {
                    Console.WriteLine("Permission denied");
                }
                catch (Exception e)
                {
                    File.Delete(overwolfCorePath);
                    if (File.Exists(backupFilePath))
                        File.Copy(backupFilePath, overwolfCorePath);
                    Console.WriteLine(e);
                }
                resolver.Dispose();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }

            if (File.Exists(overwolfBDDllPath))
            {
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfBDDllPath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };
                AssemblyDefinition overwolfSubscriptions = AssemblyDefinition.ReadAssembly(overwolfBDDllPath, reader);
                TypeDefinition overwolfSubscriptionsModel = overwolfSubscriptions.MainModule.GetType("OverWolf.Client.BL.ODKv2.Managers.DataManager.ExtensionDataManager");

                foreach (var m in overwolfSubscriptionsModel.Methods)
                {
                    if (m.Name == "BlockUnauthorizedExtension" || m.Name == "ValidateExtension" || m.Name == "IsWhiteListForValidation")
                    {
                        m.Body.Instructions.Clear();

                        m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

                    }
                }

                overwolfSubscriptions.Write(overwolfBDDllPath);
            }

            if (File.Exists(overwolfSubscriptionsPath))
            {
                bool successful = true;
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                          Overwolf.Subscriptions.dll                    ||");
                Console.WriteLine("||                                                                        ||");

                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfCoreCUPath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };
                AssemblyDefinition overwolfSubscriptions = AssemblyDefinition.ReadAssembly(overwolfSubscriptionsPath, reader);
                TypeDefinition overwolfSubscriptionsModel = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription");
                TypeDefinition overwolfSubscriptionsModelInfo = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info");
                if (overwolfSubscriptionsModel != null)
                {
                    Console.WriteLine("|| OverWolf.Subscriptions.Model.Subscription type found!                  ||");
                    foreach (var setMethod in overwolfSubscriptionsModel.Methods)
                    {
                        if (setMethod.Attributes.HasFlag(MethodAttributes.Private) && setMethod.Name.StartsWith("set_"))
                        {
                            setMethod.Attributes = setMethod.Attributes & ~MethodAttributes.Private;
                            setMethod.Attributes = setMethod.Attributes | MethodAttributes.Public;
                            Log(" -- Method " + setMethod.Name + " patched");
                        }
                    }
                    Console.WriteLine("|| OverWolf.Subscriptions.Model.Subscription patched successful!          ||");

                    Console.WriteLine("|| OverWolf.Subscriptions.Model.Subscription/Info type found!             ||");
                    foreach (var setMethod in overwolfSubscriptionsModelInfo.Methods)
                    {
                        if (setMethod.Attributes.HasFlag(MethodAttributes.Private) && setMethod.Name.StartsWith("set_"))
                        {
                            setMethod.Attributes = setMethod.Attributes & ~MethodAttributes.Private;
                            setMethod.Attributes = setMethod.Attributes | MethodAttributes.Public;
                            Log(" -- Method " + setMethod.Name + " patched");
                        }
                    }
                    Console.WriteLine("|| OverWolf.Subscriptions.Model.Subscription/Info patched successful!      ||");

                    TypeDefinition overwolfCoreProfile = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Settings.SubscriptionRepository");
                    if (overwolfCoreProfile != null)
                    {
                        Console.WriteLine("|| Overwolf.Subscriptions.Settings.SubscriptionRepository  type found!    ||");
                        MethodDefinition overwolfCoreGES = overwolfCoreProfile.Methods.SingleOrDefault(x => x.Name == "GetExtensionSubscriptions");
                        if (overwolfCoreGES != null)
                        {
                            Console.WriteLine("|| -- GetExtensionSubscriptions method found!                             ||");
                            try
                            {
                                overwolfCoreGES = InjectMethods.OverwolfSubscriptionsGetExtensionSubscriptions(ref overwolfSubscriptions, overwolfCoreGES);
                            }
                            catch (Exception e)
                            {
                                successful = false;
                                Console.WriteLine("Error, Overwolf.Subscriptions will not be patched: ");
                                Console.WriteLine(e);
                            }
                        }
                    }
                }
                string backupFilePath = Path.GetDirectoryName(overwolfSubscriptionsPath) + "\\" + Path.GetFileNameWithoutExtension(overwolfSubscriptionsPath) + "_bak.dll";

                try
                {
                    if (File.Exists(backupFilePath))
                        File.Delete(backupFilePath);
                    File.Copy(overwolfSubscriptionsPath, backupFilePath);
                    if (successful)
                        overwolfSubscriptions.Write(overwolfSubscriptionsPath);
                    Console.WriteLine("|| ------ Patched successfully                                            ||");
                }
                catch (System.UnauthorizedAccessException)
                {
                    Console.WriteLine("Permission denied");
                }
                catch (Exception e)
                {
                    File.Delete(overwolfSubscriptionsPath);
                    if (File.Exists(backupFilePath))
                        File.Copy(backupFilePath, overwolfSubscriptionsPath);
                    Console.WriteLine(e);
                }
                resolver.Dispose();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }


            if (File.Exists(overwolfCoreCUPath))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                      OverWolf.Client.CommonUtils.dll                   ||");
                Console.WriteLine("||                                                                        ||");
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
                resolver.Dispose();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }

            if (File.Exists(overwolfExtensionsDllPath))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                             OverWolf.Extensions                        ||");
                Console.WriteLine("||                                                                        ||");
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(Path.GetDirectoryName(overwolfExtensionsDllPath));
                ReaderParameters reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };
                AssemblyDefinition overwolfExtensions = AssemblyDefinition.ReadAssembly(overwolfExtensionsDllPath, reader);
                TypeDefinition overwolfExtensionsValidation = overwolfExtensions.MainModule.GetType("Overwolf.Extensions.Validation.ContentVerifiyJob");
                if (overwolfExtensionsValidation != null)
                {
                    Console.WriteLine("|| Overwolf.Extensions.Validation.ContentVerifyJob type found!             ||");
                    List<MethodDefinition> VerifyFileSyncMethods = overwolfExtensionsValidation.Methods.Where(x => x.Name == "VerifyFileSync").ToList();
                    if (VerifyFileSyncMethods.Count == 0)
                    {
                        Console.WriteLine("VerifyFileSyncMethods not found!");
                    }
                    try
                    {
                        string backupFilePath = Path.GetDirectoryName(overwolfExtensionsDllPath) + "\\" + Path.GetFileNameWithoutExtension(overwolfExtensionsDllPath) + "_bak.dll";
                        if (File.Exists(backupFilePath))
                            File.Delete(backupFilePath);
                        File.Copy(overwolfExtensionsDllPath, backupFilePath);
                    }
                    catch (System.UnauthorizedAccessException)
                    {
                        Console.WriteLine("Permission denied");
                    }
                    foreach (MethodDefinition VerifyFileSync in VerifyFileSyncMethods)
                    {
                        Console.WriteLine("|| -- VerifyFileSyncMethod found!                                         ||");
                        VerifyFileSync.Body.Variables.Clear();
                        VerifyFileSync.Body.Instructions.Clear();
                        VerifyFileSync.Body.ExceptionHandlers.Clear();
                        VerifyFileSync.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                        VerifyFileSync.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        Console.WriteLine("|| ---- VerifyFileSyncMethod method patched!                              ||");
                    }
                    overwolfExtensions.Write(overwolfExtensionsDllPath);
                    Console.WriteLine("|| ------ Patched successfully                                            ||");

                }
                else
                {
                    Console.WriteLine("OverWolf.Extensions.Validation type not found!");
                }
                resolver.Dispose();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }

            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Complete!");

            Console.ReadKey();
        }
    }
}
