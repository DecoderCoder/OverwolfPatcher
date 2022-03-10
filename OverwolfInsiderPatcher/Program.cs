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
            Console.Title = "Overwolf patcher by Decode 1.32";

            const string outplayedId = "cghphpbjeabdkomiphingnegihoigeggcfphdofo";
            const string porofessorId = "pibhbkkgefgheeglaeemkkfjlhidhcedalapdggh";
            const string orcaId = "peejkhfmgjhlnpgbkeoeakaaiicegdmoahhkfcgp";

            string overwolfPath = "";
            string overwolfDataPath = "";
            string overwolfExtensionsPath = "";
            string overwolfCorePath = "";
            string overwolfCoreCUPath = "";
            string overwolfExtensionsDllPath = ""; // Overwolf.Extensions.dll

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

                        try
                        {
                            string backupFilePath = Path.GetDirectoryName(overwolfCorePath) + "\\" + Path.GetFileNameWithoutExtension(overwolfCorePath) + "_bak.dll";
                            if (File.Exists(backupFilePath))
                                File.Delete(backupFilePath);
                            File.Copy(overwolfCorePath, backupFilePath);
                            overwolfCore.Write(overwolfCorePath);
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
                        Console.WriteLine("|| ShowInsiderBlockMessage not found!                                    ||");
                    }                 
                }
                else
                {
                    Console.WriteLine("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type not found!");
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
                TypeDefinition overwolfExtensionsValidation = overwolfExtensions.MainModule.GetType("Overwolf.Extensions.Validation.ConentVerifiyJob");
                if (overwolfExtensionsValidation != null)
                {
                    Console.WriteLine("|| Overwolf.Extensions.Validation.ConentVerifyJob type found!             ||");
                    List<MethodDefinition> VerifyFileSyncMethods = overwolfExtensionsValidation.Methods.Where(x => x.Name == "VerifyFileSync").ToList();
                    if (VerifyFileSyncMethods.Count == 0)
                    {
                        Console.WriteLine("VerifyFileSyncMethods not found!");
                    }
                    //try
                    //{
                    //    string backupFilePath = Path.GetDirectoryName(overwolfExtensionsDllPath) + "\\" + Path.GetFileNameWithoutExtension(overwolfExtensionsDllPath) + "_bak.dll";
                    //    if (File.Exists(backupFilePath))
                    //        File.Delete(backupFilePath);
                    //    File.Copy(overwolfExtensionsDllPath, backupFilePath);                        
                    //}
                    //catch (System.UnauthorizedAccessException)
                    //{
                    //    Console.WriteLine("Permission denied");
                    //}
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

            if (Directory.Exists(overwolfExtensionsPath + outplayedId))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                               Outplayed                                ||");
                Console.WriteLine("||                                                                        ||");
                string[] outplayedVersionsPath = Directory.GetDirectories(overwolfExtensionsPath + outplayedId);
                string outplayedPath = outplayedVersionsPath.LastOrDefault();
                if (String.IsNullOrEmpty(outplayedPath) == false)
                {
                    string indexScripts = File.ReadAllText(outplayedPath + "\\indexScripts.js");
                    if (indexScripts.Contains("null !== (e = null == t ? void 0 : t.includes(this._subscriptionPlan)) && void 0 !== e && e"))
                    {
                        indexScripts = indexScripts.Replace("null !== (e = null == t ? void 0 : t.includes(this._subscriptionPlan)) && void 0 !== e && e", "true"); // Делает "premium"
                                                                                                                                                                    //indexScripts = indexScripts.Replace("className: \"app-aside\"", "className: \"app-aside1\"");  // Удаляет на главной странице пустое место от рекламы // Оказывается там есть переключатель и это лишнее
                        File.WriteAllText(outplayedPath + "\\indexScripts.js", indexScripts);
                        Console.WriteLine("|| -- [indexScripts.js] Patched successfully                              ||");
                    }
                    else
                    {
                        Console.WriteLine("|| -- [indexScripts.js] Patch failed or already patched                   ||");
                    }

                    string backgroundScripts = File.ReadAllText(outplayedPath + "\\backgroundScripts.js");
                    if (backgroundScripts.Contains("return 0 !== t.length;"))
                    {
                        backgroundScripts = backgroundScripts.Replace("return 0 !== t.length;", "return true;"); // Делает "premium"
                        File.WriteAllText(outplayedPath + "\\backgroundScripts.js", backgroundScripts);
                        Console.WriteLine("|| -- [backgroundScripts.js] Patched successfully                         ||");
                    }
                    else
                    {
                        Console.WriteLine("|| -- [backgroundScripts.js] Patch failed or already patched              ||");
                    }

                    Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                }
            }

            if (Directory.Exists(overwolfExtensionsPath + porofessorId))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                               Porofessor                               ||");
                Console.WriteLine("||                                                                        ||");
                string[] porofessorVersionsPath = Directory.GetDirectories(overwolfExtensionsPath + porofessorId);
                string porofessorPath = porofessorVersionsPath.LastOrDefault();
                if (String.IsNullOrEmpty(porofessorPath) == false)
                {
                    string[] windows = Directory.GetFiles(porofessorPath + "\\windows\\");
                    foreach (string window in windows)
                    {
                        string file = File.ReadAllText(window);
                        const string adContainer = "<div id=\"ad-container\"><div id=\"ad-div\" class=\"ad\"></div></div>";
                        if (file.Contains("<div id=\"ad-container\">"))
                        {
                            file = file.Replace(adContainer, "<!-- Ad Patched -->");

                            if (!file.Contains("<!-- Ad Patched -->"))
                            {
                                string adBegin = "		<div id=\"ad-container\">";
                                string adEnd = "		</div>";

                                file = String.Join("\r\n", removeFromToRow(adBegin, file, adEnd, "<!-- Ad Patched -->"));
                                Log("-- [" + Path.GetFileName(window) + "] Patched successfuly | Method 2");
                            }
                            else
                            {
                                Log("-- [" + Path.GetFileName(window) + "] Patched successfuly | Method 1");
                            }

                            File.WriteAllText(window, file);

                        }
                        else
                        {
                            if (file.Contains("<!-- Ad Patched -->"))
                            {
                                Log("-- [" + Path.GetFileName(window) + "] Already patched");
                            }
                            else
                            {
                                Log("-- [" + Path.GetFileName(window) + "] Not patched");
                            }
                        }
                    }
                }
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            }


            if (Directory.Exists(overwolfExtensionsPath + orcaId))
            {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                Console.WriteLine("||                                  Orca                                  ||");
                Console.WriteLine("||                                                                        ||");
                string[] orcaVersionsPath = Directory.GetDirectories(overwolfExtensionsPath + orcaId);
                string orcaPath = orcaVersionsPath.LastOrDefault();
                if (String.IsNullOrEmpty(orcaPath) == false)
                {
                    if (File.Exists(orcaPath + "\\dist\\desktop\\desktop.html"))
                    {
                        string desktopFile = File.ReadAllText(orcaPath + "\\dist\\desktop\\desktop.html");
                        if (!desktopFile.Contains("<!-- Ad Patched -->"))
                        {
                            // Удаление всего блока ломает вёрстку
                            // File.WriteAllLines(orcaPath + "\\dist\\desktop\\desktop.html", removeFromToRow("      <div id=\"right\">", desktopFile, "      </div>", "<!-- Ad Patched -->"));

                            // Просто скрывает рекламу, а не убирает блок с ней
                            desktopFile = desktopFile.Replace("\n      <div id=\"right\">", "      <div id=\"right\" style=\"visibility: hidden;\"> <!-- Ad Patched -->");
                            Log("-- [desktop.html] Patched successfuly");
                        }
                        else
                        {
                            Log("-- [desktop.html] Already patched");
                        }
                        File.WriteAllText(orcaPath + "\\dist\\desktop\\desktop.html", desktopFile);
                    }

                    if (File.Exists(orcaPath + "\\dist\\in_game\\in_game.html"))
                    {
                        string inGameFile = File.ReadAllText(orcaPath + "\\dist\\in_game\\in_game.html");
                        if (!inGameFile.Contains("<!-- Ad Patched -->"))
                        {
                            // Удаление всего блока ломает вёрстку
                            // File.WriteAllLines(orcaPath + "\\dist\\in_game\\in_game.html", removeFromToRow("      <div id=\"right\">", desktopFile, "      </div>", "<!-- Ad Patched -->"));

                            // Просто скрывает рекламу, а не убирает блок с ней
                            inGameFile = inGameFile.Replace("<div id=\"ad-div\" class=\"ad-div\"></div>", "<!-- Ad Patched -->");
                            inGameFile = inGameFile.Replace("id=\"inGamePanelBackground\" hidden", "id=\"inGamePanelBackground\" style=\"width: 743px;\"");  // <div class="background" id="inGamePanelBackground" hidden="true"></div>
                            inGameFile = inGameFile.Replace("id=\"closewindowtrayingame\" class", "id=\"closewindowtrayingame\" style=\"margin-left: 630px;\" class");  // <div class="background" id="inGamePanelBackground" hidden="true"></div>
                            inGameFile = inGameFile.Replace("id=\"closeButton\" class", "id=\"closeButton\" style=\"margin-left: 630px;\" class");  // <div class="background" id="inGamePanelBackground" hidden="true"></div>
                            inGameFile = inGameFile.Replace("id=\"minimazeButton\" class", "id=\"minimazeButton\" style=\"margin-left: 667px;\" class");  // <div class="background" id="inGamePanelBackground" hidden="true"></div>

                            Log("-- [in_game.html] Patched successfuly");
                        }
                        else
                        {
                            Log("-- [in_game.html] Already patched");
                        }
                        File.WriteAllText(orcaPath + "\\dist\\in_game\\in_game.html", inGameFile);
                    }
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
