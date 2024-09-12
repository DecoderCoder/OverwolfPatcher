using System.IO;
using System;
using OverwolfPatcher.Classes;
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Linq;
using Bluscream;
using Mono.Cecil.Rocks;
using System.Collections.Generic;

namespace OverwolfPatcher.Patches;

internal class ClientCommonUtils : IPatch
{

    public string Name => "Client Common Utils";
    public string Description => "Patching the overwolf client common utils dll";
    public string RelativePath => "OverWolf.Client.CommonUtils.dll";

    public bool TryPatch(Overwolf ow, out Exception error)
    {
        error = null;
        foreach (var versionFolder in ow.ProgramVersionFolders)
        {
            try
            {
                var fullPath = versionFolder.CombineFile(RelativePath);
                var fileString = $"\"{versionFolder.Name}\\{RelativePath}\"";
                if (!fullPath.Exists)
                {
                    Console.WriteLine($"{fileString} does not exist, skipping...");
                }
                this.PrintHeader();
                var resolver = new DefaultAssemblyResolver();
                resolver.AddSearchDirectory(versionFolder.FullName);
                var reader = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true, ReadingMode = ReadingMode.Immediate, InMemory = true };

                AssemblyDefinition overwolfCore = AssemblyDefinition.ReadAssembly(fullPath.FullName, reader);
                TypeDefinition overwolfCoreCUFeatures = overwolfCore.MainModule.GetType("OverWolf.Client.CommonUtils.Features.CommonFeatures");
                if (overwolfCoreCUFeatures != null)
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Client.CommonUtils.Features.CommonFeatures type found!"));
                    MethodDefinition enableDevToolsForQA = overwolfCoreCUFeatures.Methods.SingleOrDefault(x => x.Name == "EnableDevToolsForQA");
                    if (enableDevToolsForQA != null)
                    {
                        enableDevToolsForQA.PatchReturnBool(true);
                    } else
                    {
                        Console.WriteLine(Utils.Pad("EnableDevToolsForQA not found!"));
                        Console.WriteLine("");
                    }

                } else
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Client.CommonUtils.Features.CommonFeatures type not found!"));
                }

                try
                {
                    fullPath.Backup(true);
                    overwolfCore.Write(fullPath.FullName);
                    Console.WriteLine(Utils.Pad("Patched successfully"));
                } catch (UnauthorizedAccessException) { Console.WriteLine($"Permission denied for file {fileString}"); } catch (Exception e)
                {
                    fullPath.Restore();
                    Console.WriteLine(e);
                }
                resolver.Dispose();
                Console.WriteLine("||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            } catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }
        return true;
    }

    public bool TryUnpatch(Overwolf ow, out Exception error)
    {
        error = new NotImplementedException($"Unpatching is not implemented for {Name}");
        return false;
    }

}