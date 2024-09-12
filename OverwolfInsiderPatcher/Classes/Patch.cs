using Bluscream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OverwolfPatcher.Classes
{
    public interface IPatch
    {
        public string Name { get; }
        public string Description { get; }
        public string RelativePath { get; }

        public bool TryPatch(Overwolf overwolfDir, out Exception error);
        public bool TryUnpatch(Overwolf overwolfDir, out Exception error);
    }
    public static class PatchExtension // : IPatch
    {
        public static void PrintHeader(this IPatch patch)
        {
            Console.WriteLine();
            Console.WriteLine();
            var padding = Utils.GetPadding("");
            var box = new string('=', padding[2]);
            Console.WriteLine(box);
            Console.WriteLine(Utils.Pad(patch.Name));
            Console.WriteLine(Utils.Pad(patch.Description));
            Console.WriteLine(box);
        }

    }
}
