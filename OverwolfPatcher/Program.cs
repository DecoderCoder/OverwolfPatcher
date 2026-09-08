using System;

namespace OverwolfPatcher
{
    class Program
    {
        static int Main(string[] args)
        {
            try { return Testing.PremiumCommand.Run(args); }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAILED: " + error.Message);
                return 1;
            }
        }
    }
}
