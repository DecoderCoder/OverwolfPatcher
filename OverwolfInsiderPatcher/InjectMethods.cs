using Bluscream;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OverwolfPatcher
{
    internal static class InjectExtensions
    {
        public static void PatchReturn(this MethodDefinition method) => method.PatchReturnBool(null);
        public static void PatchReturnBool(this MethodDefinition method, bool? retVal)
        {
            Console.WriteLine(Utils.Pad($"Method {method.FullName} found!"));

            method.Body.ExceptionHandlers.Clear();
            method.Body.Variables.Clear();
            method.Body.Instructions.Clear();

            if (retVal.HasValue) method.Body.Instructions.Add(Instruction.Create(retVal.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            Console.WriteLine(Utils.Pad($"Method {method.FullName} patched! (returns {(retVal.HasValue ? retVal?.ToString() : "null")})"));
        }
    }
    internal class InjectMethods
    {
    }
}
