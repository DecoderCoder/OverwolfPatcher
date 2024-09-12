using Bluscream;
using Mono.Cecil;
using OverwolfPatcher.Classes;
using System;
using System.Linq;

namespace OverwolfPatcher.Patches;

internal class ClientBL : IPatch
{

    public string Name => "Client BL";
    public string Description => "Patching the overwolf client bl dll";
    public string RelativePath => "OverWolf.Client.BL.dll";

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

                AssemblyDefinition overwolfBD = AssemblyDefinition.ReadAssembly(fullPath.FullName, reader);
                TypeDefinition overwolfExtensionDataManager = overwolfBD.MainModule.GetType("OverWolf.Client.BL.ODKv2.Managers.DataManager.ExtensionDataManager");

                if (overwolfBD != null)
                {
                    MethodDefinition validateExtensionMethod = overwolfExtensionDataManager.Methods.SingleOrDefault(x => x.Name == "ValidateExtension");
                    if (validateExtensionMethod != null)
                    {
                        validateExtensionMethod.PatchReturnBool(true);
                    } else
                        Console.WriteLine("ValidateExtension not found!");

                    MethodDefinition blockUnauthorizedExtensionMethod = overwolfExtensionDataManager.Methods.SingleOrDefault(x => x.Name == "BlockUnauthorizedExtension");
                    if (blockUnauthorizedExtensionMethod != null)
                    {
                        blockUnauthorizedExtensionMethod.PatchReturnBool(false);
                    } else
                        Console.WriteLine("BlockUnauthorizedExtension not found!");

                    MethodDefinition isWhiteListForValidationMethod = overwolfExtensionDataManager.Methods.SingleOrDefault(x => x.Name == "IsWhiteListForValidation");
                    if (isWhiteListForValidationMethod != null)
                    {
                        isWhiteListForValidationMethod.PatchReturnBool(true);
                    } else
                        Console.WriteLine("IsWhiteListForValidation not found!");
                }

                try
                {
                    fullPath.Backup(true);
                    overwolfBD.Write(fullPath.FullName);
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