using Bluscream;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using OverwolfPatcher.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OverwolfPatcher.Patches;

internal class Subscriptions : IPatch
{

    public string Name => "Subscriptions";
    public string Description => "Patching the overwolf subscriptions dll";
    public string RelativePath => "Overwolf.Subscriptions.dll";

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

                AssemblyDefinition overwolfSubscriptions = AssemblyDefinition.ReadAssembly(fullPath.FullName, reader);
                TypeDefinition overwolfSubscriptionsModel = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription");
                TypeDefinition overwolfSubscriptionsModelInfo = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info");
                if (overwolfSubscriptionsModel != null)
                {
                    Console.WriteLine(Utils.Pad("OverWolf.Subscriptions.Model.Subscription type found!"));
                    foreach (var setMethod in overwolfSubscriptionsModel.Methods)
                    {
                        if (setMethod.Attributes.HasFlag(MethodAttributes.Private) && setMethod.Name.StartsWith("set_"))
                        {
                            setMethod.Attributes = setMethod.Attributes & ~MethodAttributes.Private;
                            setMethod.Attributes = setMethod.Attributes | MethodAttributes.Public;
                        }
                    }
                    Console.WriteLine(Utils.Pad("OverWolf.Subscriptions.Model.Subscription patched successful!"));

                    Console.WriteLine(Utils.Pad("OverWolf.Subscriptions.Model.Subscription/Info type found!"));
                    foreach (var setMethod in overwolfSubscriptionsModelInfo.Methods)
                    {
                        if (setMethod.Attributes.HasFlag(MethodAttributes.Private) && setMethod.Name.StartsWith("set_"))
                        {
                            setMethod.Attributes = setMethod.Attributes & ~MethodAttributes.Private;
                            setMethod.Attributes = setMethod.Attributes | MethodAttributes.Public;
                        }
                    }
                    Console.WriteLine(Utils.Pad("OverWolf.Subscriptions.Model.Subscription/Info patched successful!"));

                    TypeDefinition overwolfCoreProfile = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Settings.SubscriptionRepository");
                    if (overwolfCoreProfile != null)
                    {
                        Console.WriteLine(Utils.Pad("Overwolf.Subscriptions.Settings.SubscriptionRepository  type found!"));
                        MethodDefinition overwolfCoreGES = overwolfCoreProfile.Methods.SingleOrDefault(x => x.Name == "GetExtensionSubscriptions");
                        if (overwolfCoreGES != null)
                        {
                            try
                            {
                                overwolfCoreGES = OverwolfSubscriptionsGetExtensionSubscriptions(ref overwolfSubscriptions, overwolfCoreGES);
                            } catch (Exception e)
                            {
                                Console.WriteLine("Error, Overwolf.Subscriptions will not be patched: ");
                                Console.WriteLine(e);
                            }
                        }
                    }
                }

                try
                {
                    fullPath.Backup(true);
                    overwolfSubscriptions.Write(fullPath.FullName);
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

    public static MethodDefinition OverwolfSubscriptionsGetExtensionSubscriptions(ref AssemblyDefinition overwolfSubscriptions, MethodDefinition overwolfCoreGES)
    {
        overwolfCoreGES.Body.ExceptionHandlers.Clear();
        overwolfCoreGES.Body.Variables.Clear();
        overwolfCoreGES.Body.Instructions.Clear();
        GenericInstanceType list = new GenericInstanceType(overwolfSubscriptions.MainModule.ImportReference(typeof(List<>))); //overwolfCore.MainModule.Import();
        TypeReference SubscriptionRef = overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription");
        if (SubscriptionRef != null) // Get already imported class instead of import Overwolf.ODK.Common
        {
            list.GenericArguments.Add(SubscriptionRef);
            VariableDefinition iV = new VariableDefinition(overwolfSubscriptions.MainModule.ImportReference(typeof(int)));
            VariableDefinition dapV = new VariableDefinition(SubscriptionRef); // List<DetailedActivePlan>
            overwolfCoreGES.Body.Variables.Add(iV);
            overwolfCoreGES.Body.Variables.Add(dapV);

            TypeReference List = overwolfSubscriptions.MainModule.ImportReference(overwolfSubscriptions.MainModule.ImportReference(Type.GetType("System.Collections.Generic.List`1")).MakeGenericInstanceType(new TypeReference[] { SubscriptionRef }));
            MethodDefinition listCtor = List.Resolve().Methods.First(x => x.Name == ".ctor");
            var listCtorRef = overwolfSubscriptions.MainModule.ImportReference(listCtor, List);
            listCtorRef.DeclaringType = List;
            MethodDefinition listAdd = List.Resolve().Methods.First(x => x.Name == "Add");
            var listAddRef = overwolfSubscriptions.MainModule.ImportReference(listAdd, List);
            listAddRef.DeclaringType = List;


            overwolfCoreGES.Body.SimplifyMacros();
            for (int i = 0; i < 73; i++)
            {
                overwolfCoreGES.Body.Instructions.Add(Instruction.Create(OpCodes.Nop));
            }
            overwolfCoreGES.Body.Instructions[0] = Instruction.Create(OpCodes.Ldarg_S, overwolfCoreGES.Parameters[3]);
            overwolfCoreGES.Body.Instructions[1] = Instruction.Create(OpCodes.Ldnull);
            overwolfCoreGES.Body.Instructions[2] = Instruction.Create(OpCodes.Stind_Ref);
            overwolfCoreGES.Body.Instructions[3] = Instruction.Create(OpCodes.Ldarg_0);
            overwolfCoreGES.Body.Instructions[4] = Instruction.Create(OpCodes.Call, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Settings.SubscriptionRepository").Methods.First(x => x.Name == "IsRequiredServiceUnavailable"));
            //
            overwolfCoreGES.Body.Instructions[6] = Instruction.Create(OpCodes.Ldc_I4_0);
            overwolfCoreGES.Body.Instructions[7] = Instruction.Create(OpCodes.Ret);
            overwolfCoreGES.Body.Instructions[8] = Instruction.Create(OpCodes.Ldarg_1);
            overwolfCoreGES.Body.Instructions[9] = Instruction.Create(OpCodes.Call, overwolfSubscriptions.MainModule.ImportReference(typeof(String).GetMethod("IsNullOrEmpty")));
            overwolfCoreGES.Body.Instructions[11] = Instruction.Create(OpCodes.Ldc_I4_0);
            overwolfCoreGES.Body.Instructions[12] = Instruction.Create(OpCodes.Ret);
            overwolfCoreGES.Body.Instructions[13] = Instruction.Create(OpCodes.Ldarg_S, overwolfCoreGES.Parameters[3]);
            overwolfCoreGES.Body.Instructions[14] = Instruction.Create(OpCodes.Newobj, listCtorRef);
            overwolfCoreGES.Body.Instructions[15] = Instruction.Create(OpCodes.Stind_Ref);
            overwolfCoreGES.Body.Instructions[16] = Instruction.Create(OpCodes.Ldc_I4_0);
            overwolfCoreGES.Body.Instructions[17] = Instruction.Create(OpCodes.Stloc_0);


            overwolfCoreGES.Body.Instructions[19] = Instruction.Create(OpCodes.Newobj, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == ".ctor"));
            overwolfCoreGES.Body.Instructions[20] = Instruction.Create(OpCodes.Stloc_1);
            overwolfCoreGES.Body.Instructions[21] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[22] = Instruction.Create(OpCodes.Ldc_I4_0);
            overwolfCoreGES.Body.Instructions[23] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_Expired"));
            overwolfCoreGES.Body.Instructions[24] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[25] = Instruction.Create(OpCodes.Ldloc_0);
            overwolfCoreGES.Body.Instructions[26] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_PlanId"));
            overwolfCoreGES.Body.Instructions[27] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[28] = Instruction.Create(OpCodes.Ldc_I8, 1735682400000);
            overwolfCoreGES.Body.Instructions[29] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_Expiry"));
            overwolfCoreGES.Body.Instructions[30] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[31] = Instruction.Create(OpCodes.Newobj, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info").Methods.First(x => x.Name == ".ctor"));
            overwolfCoreGES.Body.Instructions[32] = Instruction.Create(OpCodes.Dup);
            overwolfCoreGES.Body.Instructions[33] = Instruction.Create(OpCodes.Ldstr, "all questions -> https://t.me/DecoderCoder");
            overwolfCoreGES.Body.Instructions[34] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info").Methods.First(x => x.Name == "set_Description"));
            overwolfCoreGES.Body.Instructions[35] = Instruction.Create(OpCodes.Dup);
            overwolfCoreGES.Body.Instructions[36] = Instruction.Create(OpCodes.Ldstr, "Decode");
            overwolfCoreGES.Body.Instructions[37] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info").Methods.First(x => x.Name == "set_Title"));
            overwolfCoreGES.Body.Instructions[38] = Instruction.Create(OpCodes.Dup);
            overwolfCoreGES.Body.Instructions[39] = Instruction.Create(OpCodes.Ldc_I4, 999);
            overwolfCoreGES.Body.Instructions[40] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info").Methods.First(x => x.Name == "set_PeriodMonths"));
            overwolfCoreGES.Body.Instructions[41] = Instruction.Create(OpCodes.Dup);
            overwolfCoreGES.Body.Instructions[42] = Instruction.Create(OpCodes.Ldc_I4, 1);
            overwolfCoreGES.Body.Instructions[43] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription/Info").Methods.First(x => x.Name == "set_Price"));
            overwolfCoreGES.Body.Instructions[44] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_Plan"));
            overwolfCoreGES.Body.Instructions[45] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[46] = Instruction.Create(OpCodes.Ldc_I4_0);
            overwolfCoreGES.Body.Instructions[47] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_State"));

            overwolfCoreGES.Body.Instructions[48] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[49] = Instruction.Create(OpCodes.Ldarg_2);
            overwolfCoreGES.Body.Instructions[50] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_ExtensionId"));
            overwolfCoreGES.Body.Instructions[51] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[52] = Instruction.Create(OpCodes.Ldstr, "---");
            overwolfCoreGES.Body.Instructions[53] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_MUID"));
            overwolfCoreGES.Body.Instructions[54] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[55] = Instruction.Create(OpCodes.Ldstr, "---");
            overwolfCoreGES.Body.Instructions[56] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_Token"));

            overwolfCoreGES.Body.Instructions[57] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[58] = Instruction.Create(OpCodes.Ldarg_1);
            overwolfCoreGES.Body.Instructions[59] = Instruction.Create(OpCodes.Callvirt, overwolfSubscriptions.MainModule.GetType("Overwolf.Subscriptions.Model.Subscription").Methods.First(x => x.Name == "set_Username"));
            overwolfCoreGES.Body.Instructions[60] = Instruction.Create(OpCodes.Ldarg_S, overwolfCoreGES.Parameters[3]);
            overwolfCoreGES.Body.Instructions[61] = Instruction.Create(OpCodes.Ldind_Ref);
            overwolfCoreGES.Body.Instructions[62] = Instruction.Create(OpCodes.Ldloc_1);
            overwolfCoreGES.Body.Instructions[63] = Instruction.Create(OpCodes.Callvirt, listAddRef);
            overwolfCoreGES.Body.Instructions[64] = Instruction.Create(OpCodes.Ldloc_0);
            overwolfCoreGES.Body.Instructions[65] = Instruction.Create(OpCodes.Ldc_I4_1);
            overwolfCoreGES.Body.Instructions[66] = Instruction.Create(OpCodes.Add);
            overwolfCoreGES.Body.Instructions[67] = Instruction.Create(OpCodes.Stloc_0);
            overwolfCoreGES.Body.Instructions[68] = Instruction.Create(OpCodes.Ldloc_0);
            overwolfCoreGES.Body.Instructions[69] = Instruction.Create(OpCodes.Ldc_I4, 9999);
            overwolfCoreGES.Body.Instructions[70] = Instruction.Create(OpCodes.Blt, overwolfCoreGES.Body.Instructions[19]);

            overwolfCoreGES.Body.Instructions[71] = Instruction.Create(OpCodes.Ldc_I4_1);
            overwolfCoreGES.Body.Instructions[72] = Instruction.Create(OpCodes.Ret);


            overwolfCoreGES.Body.Instructions[5] = Instruction.Create(OpCodes.Brfalse_S, overwolfCoreGES.Body.Instructions[8]);
            overwolfCoreGES.Body.Instructions[10] = Instruction.Create(OpCodes.Brfalse_S, overwolfCoreGES.Body.Instructions[13]);
            overwolfCoreGES.Body.Instructions[18] = Instruction.Create(OpCodes.Br, overwolfCoreGES.Body.Instructions[68]); //
            overwolfCoreGES.Body.OptimizeMacros();
        }
        return overwolfCoreGES;
    }

}