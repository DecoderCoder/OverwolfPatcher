using Bluscream;
using Mono.Cecil;
using OverwolfPatcher.Classes;
using System;
using System.Linq;

namespace OverwolfPatcher.Patches;

internal class Extensions : IPatch
{

    public string Name => "Extensions";
    public string Description => "Patching the overwolf extensions dll";
    public string RelativePath => "OverWolf.Extensions.dll";

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

                var overwolfExtensions = AssemblyDefinition.ReadAssembly(fullPath.FullName, reader);
                var overwolfExtensionsValidation = overwolfExtensions.MainModule.GetType("Overwolf.Extensions.Validation.ContentVerifiyJob");
                if (overwolfExtensionsValidation != null)
                {
                    Console.WriteLine(Utils.Pad("Overwolf.Extensions.Validation.ContentVerifyJob type found!"));
                    var VerifyFileSyncMethods = overwolfExtensionsValidation.Methods.Where(x => x.Name == "VerifyFileSync").ToList();
                    if (VerifyFileSyncMethods.Count == 0)
                    {
                        Console.WriteLine(Utils.Pad("VerifyFileSyncMethods not found!"));
                    }
                    foreach (var VerifyFileSync in VerifyFileSyncMethods)
                    {
                        VerifyFileSync.PatchReturnBool(false);
                    }
                } else
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Extensions.Validation type not found!"));
                }

                try
                {
                    fullPath.Backup(true);
                    overwolfExtensions.Write(fullPath.FullName);
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