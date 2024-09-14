using Bluscream;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using OverwolfPatcher.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OverwolfPatcher.Patches;

internal class ClientCore : IPatch
{

    public string Name => "Client Core";
    public string Description => "Patching the overwolf client core dll";
    public string RelativePath => "OverWolf.Client.Core.dll";

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
                TypeDefinition overwolfCoreWManager = overwolfCore.MainModule.GetType("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper");
                if (overwolfCoreWManager != null)
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type found!"));
                    MethodDefinition showInsiderBlockMessageMethod = overwolfCoreWManager.Methods.SingleOrDefault(x => x.Name == "ShowInsiderBlockMessage");
                    if (showInsiderBlockMessageMethod != null)
                    {

                        showInsiderBlockMessageMethod.PatchReturnBool(false);

                        TypeDefinition overwolfCoreIU = overwolfCore.MainModule.GetType("OverWolf.Client.Core.ODKv2.OverwolfInternalUtils");
                        if (overwolfCoreIU != null)
                        {
                            MethodDefinition overwolfCoreGPI = overwolfCoreIU.Methods.SingleOrDefault(x => x.Name == "getProductInformation");
                            if (overwolfCoreGPI != null)
                            {
                                var patchedMsg = $" (Patched by {AssemblyInfo.Product})";
                                foreach (Instruction instr in overwolfCoreGPI.Body.Instructions)
                                {
                                    if (instr.Operand != null && instr.Operand.GetType() == typeof(string) && ((string)instr.Operand).StartsWith("Copyright Overwolf © ") && !((string)instr.Operand).EndsWith(patchedMsg))
                                    {
                                        instr.Operand += patchedMsg;
                                    }
                                }
                            }
                        }
                    } else
                    {
                        Console.WriteLine(Utils.Pad("ShowInsiderBlockMessage not found!"));
                    }

                    TypeDefinition overwolfCoreProfile = overwolfCore.MainModule.GetType("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription");
                    if (overwolfCoreProfile != null)
                    {
                        Console.WriteLine(Utils.Pad("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription type found!"));
                        MethodDefinition overwolfCoreGES = overwolfCoreProfile.Methods.SingleOrDefault(x => x.Name == "GetExtensionSubscriptions");
                        if (overwolfCoreGES != null)
                        {
                            Console.WriteLine(Utils.Pad("GetExtensionSubscriptions method found!"));
                            try
                            {
                                overwolfCoreGES = OverwolfCoreGetExtensionSubscriptions(ref overwolfCore, overwolfCoreGES);
                            } catch (Exception e)
                            {
                                Console.WriteLine("Error, Overwolf.Core will not be patched: ");
                                Console.WriteLine(e);
                            }
                        }
                    }

                } else
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Client.Core.Managers.WindowsInsiderSupportHelper type not found!"));
                }


                TypeDefinition overwolfCoreExtensionWebApp = overwolfCore.MainModule.GetType("OverWolf.Client.Core.ODKv2.ExtensionWebApp"); // you think this will already work? i can try yes
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Client.Core.ODKv2.ExtensionWebApp type found!"));
                    MethodDefinition overwolfCoreExtensionWebAppStratContentValidation = overwolfCoreExtensionWebApp.Methods.SingleOrDefault(x => x.Name == "StratContentValidation");
                    if (overwolfCoreExtensionWebAppStratContentValidation != null)
                    {
                        try
                        {
                            overwolfCoreExtensionWebAppStratContentValidation.PatchReturn();
                        } catch (Exception e)
                        {
                            Console.WriteLine("Error, Overwolf.Core will not be patched: ");
                            Console.WriteLine(e);
                        }
                    }
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

    public static MethodDefinition OverwolfCoreGetExtensionSubscriptions(ref AssemblyDefinition overwolfCore, MethodDefinition overwolfCoreGES)
    {
        overwolfCoreGES.Body.ExceptionHandlers.Clear();
        overwolfCoreGES.Body.Variables.Clear();
        overwolfCoreGES.Body.Instructions.Clear();
        GenericInstanceType list = new GenericInstanceType(overwolfCore.MainModule.ImportReference(typeof(List<>))); //overwolfCore.MainModule.Import();
        TypeReference DetailedActivePlan;

        if (overwolfCore.MainModule.TryGetTypeReference("ODKv2API.DetailedActivePlan", out DetailedActivePlan)) // Get already imported class instead of import Overwolf.ODK.Common
        {
            TypeDefinition DetailedActivePlanDef = DetailedActivePlan.Resolve();
            list.GenericArguments.Add(DetailedActivePlan);
            VariableDefinition dapV = new VariableDefinition(list); // List<DetailedActivePlan>
            VariableDefinition iV = new VariableDefinition(overwolfCore.MainModule.ImportReference(typeof(int)));
            overwolfCoreGES.Body.Variables.Add(dapV);
            overwolfCoreGES.Body.Variables.Add(iV);

            {

                TypeReference List = overwolfCore.MainModule.ImportReference(overwolfCore.MainModule.ImportReference(Type.GetType("System.Collections.Generic.List`1")).MakeGenericInstanceType(new TypeReference[] { DetailedActivePlan }));


                MethodDefinition listCtor = List.Resolve().Methods.First(x => x.Name == ".ctor");
                var listCtorRef = overwolfCore.MainModule.ImportReference(listCtor, List);
                listCtorRef.DeclaringType = List;
                MethodDefinition listToArray = List.Resolve().Methods.First(x => x.Name == "ToArray");
                var listToArrayRef = overwolfCore.MainModule.ImportReference(listToArray);
                listToArrayRef.DeclaringType = List;
                MethodDefinition listAdd = List.Resolve().Methods.First(x => x.Name == "Add");
                var listlistAddRef = overwolfCore.MainModule.ImportReference(listAdd);
                listlistAddRef.DeclaringType = List;




                overwolfCoreGES.Body.SimplifyMacros();
                for (int i = 0; i < 39; i++)
                {
                    overwolfCoreGES.Body.Instructions.Add(Instruction.Create(OpCodes.Nop));
                }
                overwolfCoreGES.Body.Instructions[0] = (Instruction.Create(OpCodes.Newobj, listCtorRef));
                overwolfCoreGES.Body.Instructions[1] = (Instruction.Create(OpCodes.Stloc_0));
                overwolfCoreGES.Body.Instructions[2] = (Instruction.Create(OpCodes.Ldc_I4_0));
                overwolfCoreGES.Body.Instructions[3] = (Instruction.Create(OpCodes.Stloc_1));
                overwolfCoreGES.Body.Instructions[5] = (Instruction.Create(OpCodes.Ldloc_0));
                overwolfCoreGES.Body.Instructions[6] = (Instruction.Create(OpCodes.Newobj, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == ".ctor")))); // DetailedActivePlanR constructor
                overwolfCoreGES.Body.Instructions[7] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[8] = (Instruction.Create(OpCodes.Ldc_R4, 1.0f));
                overwolfCoreGES.Body.Instructions[9] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_Price"))));
                overwolfCoreGES.Body.Instructions[10] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[11] = (Instruction.Create(OpCodes.Ldloc_1));
                overwolfCoreGES.Body.Instructions[12] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_PlanId"))));
                overwolfCoreGES.Body.Instructions[13] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[14] = (Instruction.Create(OpCodes.Ldstr, "All questions -> https://t.me/DecoderCoder"));
                overwolfCoreGES.Body.Instructions[15] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_Description"))));
                overwolfCoreGES.Body.Instructions[16] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[17] = (Instruction.Create(OpCodes.Ldc_I4_0));
                overwolfCoreGES.Body.Instructions[18] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_State"))));
                overwolfCoreGES.Body.Instructions[19] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[20] = (Instruction.Create(OpCodes.Ldstr, "cracked by Decode"));
                overwolfCoreGES.Body.Instructions[21] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_Title"))));
                overwolfCoreGES.Body.Instructions[22] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[23] = (Instruction.Create(OpCodes.Ldc_I4, 9999));
                overwolfCoreGES.Body.Instructions[24] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_PeriodMonths"))));
                overwolfCoreGES.Body.Instructions[25] = (Instruction.Create(OpCodes.Dup));
                overwolfCoreGES.Body.Instructions[26] = (Instruction.Create(OpCodes.Ldc_I8, 32511218423000));
                overwolfCoreGES.Body.Instructions[27] = (Instruction.Create(OpCodes.Callvirt, overwolfCore.MainModule.ImportReference(DetailedActivePlanDef.Methods.First(x => x.Name == "set_ExpiryDate"))));
                overwolfCoreGES.Body.Instructions[28] = (Instruction.Create(OpCodes.Callvirt, listlistAddRef));
                overwolfCoreGES.Body.Instructions[29] = (Instruction.Create(OpCodes.Ldloc_1));
                overwolfCoreGES.Body.Instructions[30] = (Instruction.Create(OpCodes.Ldc_I4_1));
                overwolfCoreGES.Body.Instructions[31] = (Instruction.Create(OpCodes.Add));
                overwolfCoreGES.Body.Instructions[32] = (Instruction.Create(OpCodes.Stloc_1));
                overwolfCoreGES.Body.Instructions[33] = (Instruction.Create(OpCodes.Ldloc_1));
                overwolfCoreGES.Body.Instructions[34] = (Instruction.Create(OpCodes.Ldc_I4, 9999));
                overwolfCoreGES.Body.Instructions[35] = (Instruction.Create(OpCodes.Blt_S, overwolfCoreGES.Body.Instructions[5]));
                overwolfCoreGES.Body.Instructions[36] = (Instruction.Create(OpCodes.Ldloc_0));
                overwolfCoreGES.Body.Instructions[37] = (Instruction.Create(OpCodes.Callvirt, listToArrayRef));
                overwolfCoreGES.Body.Instructions[38] = (Instruction.Create(OpCodes.Ret));

                overwolfCoreGES.Body.Instructions[4] = (Instruction.Create(OpCodes.Br_S, overwolfCoreGES.Body.Instructions[33]));
                overwolfCoreGES.Body.OptimizeMacros();
            }
        }

        return overwolfCoreGES;
    }
}