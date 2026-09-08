using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OverwolfPatcher.Testing
{
    // The legacy API only. This does not change login, payment, or server entitlements.
    internal static class PremiumAssembly
    {
        internal const string FileName = "OverWolf.Client.Core.dll";
        internal const string Marker = "OverwolfPatcher.PremiumTest.v1";

        internal static void Rewrite(AssemblyDefinition assembly, string appId, int[] plans)
        {
            if (assembly.Name.HasPublicKey || (assembly.MainModule.Attributes & ModuleAttributes.StrongNameSigned) != 0)
                throw new NotSupportedException("The target is strong-name signed. Refusing to invalidate its signature.");
            if (plans.Length == 0 || plans.Any(p => p <= 0)) throw new ArgumentException("Positive plan IDs are required.");
            var module = assembly.MainModule;
            if (module.Resources.Any(r => r.Name == Marker))
                throw new InvalidOperationException("Already patched. Restore this test before changing its plans.");
            var type = module.GetType("OverWolf.Client.Core.ODKv2.Profile.OverwolfSubscription")
                ?? throw new NotSupportedException("Legacy subscription API type is missing.");
            var detailed = Find(type, "GetExtensionSubscriptions", "ODKv2API.DetailedActivePlan[]");
            var ids = Find(type, "GetExtensionSubscriptionsIds", "System.Int32[]");
            var uid = detailed.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>()
                .FirstOrDefault(m => m.Name == "get_UID" && m.ReturnType.FullName == "System.String" && m.Parameters.Count == 0 && m.HasThis)
                ?? throw new NotSupportedException("The subscription API no longer exposes the expected app ID getter.");
            var planType = ((ArrayType)detailed.ReturnType).ElementType;
            var definition = planType.Resolve();
            var constructor = definition.Methods.Single(m => m.IsConstructor && !m.IsStatic && m.IsPublic && m.Parameters.Count == 0);
            var setters = new[] {
                Setter(definition, "PlanId", "System.Int32"), Setter(definition, "State", "ODKv2API.SubscriptionState"),
                Setter(definition, "ExpiryDate", "System.Int64"), Setter(definition, "Title", "System.String"),
                Setter(definition, "Description", "System.String"), Setter(definition, "Price", "System.Double"),
                Setter(definition, "PeriodMonths", "System.Int32")
            };
            var state = setters[1].Parameters[0].ParameterType.Resolve().Fields
                .Single(f => f.HasConstant && string.Equals(f.Name, "Active", StringComparison.OrdinalIgnoreCase));
            var expiry = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds();
            // Resolve and validate every dependency before touching either body.
            var equals = module.ImportReference(typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) }));
            var importedCtor = module.ImportReference(constructor);
            var importedSetters = setters.Select(s => module.ImportReference(s)).ToArray();
            foreach (var method in new[] { detailed, ids })
            {
                var originalEntry = method.Body.Instructions[0];
                var instructions = new System.Collections.Generic.List<Instruction> {
                    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, uid),
                    Instruction.Create(OpCodes.Ldstr, appId), Instruction.Create(OpCodes.Call, equals),
                    Instruction.Create(OpCodes.Brfalse, originalEntry), Instruction.Create(OpCodes.Ldc_I4, plans.Length),
                    Instruction.Create(OpCodes.Newarr, method == ids ? module.TypeSystem.Int32 : planType)
                };
                for (int i = 0; i < plans.Length; i++)
                {
                    instructions.Add(Instruction.Create(OpCodes.Dup));
                    instructions.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                    if (method == ids) instructions.Add(Instruction.Create(OpCodes.Ldc_I4, plans[i]));
                    else
                    {
                        instructions.Add(Instruction.Create(OpCodes.Newobj, importedCtor));
                        var values = new[] {
                            Instruction.Create(OpCodes.Ldc_I4, plans[i]), Instruction.Create(OpCodes.Ldc_I4, Convert.ToInt32(state.Constant)),
                            Instruction.Create(OpCodes.Ldc_I8, expiry), Instruction.Create(OpCodes.Ldstr, "Local premium test"),
                            Instruction.Create(OpCodes.Ldstr, "Seven-day local API fixture; no server entitlement"),
                            Instruction.Create(OpCodes.Ldc_R8, 0d), Instruction.Create(OpCodes.Ldc_I4, 1)
                        };
                        for (int j = 0; j < values.Length; j++)
                        {
                            instructions.Add(Instruction.Create(OpCodes.Dup));
                            instructions.Add(values[j]);
                            instructions.Add(Instruction.Create(OpCodes.Callvirt, importedSetters[j]));
                        }
                    }
                    instructions.Add(Instruction.Create(method == ids ? OpCodes.Stelem_I4 : OpCodes.Stelem_Ref));
                }
                instructions.Add(Instruction.Create(OpCodes.Ret));
                var il = method.Body.GetILProcessor();
                foreach (var instruction in instructions) il.InsertBefore(originalEntry, instruction);
                method.Body.MaxStackSize = Math.Max(method.Body.MaxStackSize, 8);
            }
            module.Resources.Add(new EmbeddedResource(Marker, ManifestResourceAttributes.Private,
                System.Text.Encoding.UTF8.GetBytes(appId + ":" + string.Join(",", plans))));
        }

        static MethodDefinition Find(TypeDefinition type, string name, string returns)
        {
            var method = type.Methods.SingleOrDefault(m => m.Name == name && m.Parameters.Count == 0 && m.ReturnType.FullName == returns && !m.IsStatic);
            if (method == null || !method.HasBody || method.Body.Instructions.Count == 0)
                throw new NotSupportedException("Unsupported subscription method: " + name);
            return method;
        }

        static MethodDefinition Setter(TypeDefinition type, string name, string parameter) =>
            type.Methods.Single(m => m.Name == "set_" + name && m.IsPublic && !m.IsStatic &&
                m.ReturnType.FullName == "System.Void" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == parameter);
    }
}
