using System;
using System.Reflection;

namespace OverwolfPatcher.Runtime
{
    // Kept as a compatibility type for older test fixtures and configuration
    // files. The patcher no longer changes CLR method pointers from managed
    // code; use the process-scoped native profiler launcher instead.
    public sealed class PremiumAppDomainManager : AppDomainManager
    {
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);
            PremiumRuntime.StartFromEnvironment();
        }
    }

    public static class PremiumRuntime
    {
        public static void StartFromEnvironment()
        {
            // Deliberately empty. An AppDomainManager cannot safely replace a
            // method body after the CLR has prepared it, and the former
            // MethodHandle pointer redirect has been retired.
        }

        public static bool Install(Assembly assembly, string targetAppId, int[] targetPlans, string diagnosticPath = null)
        {
            throw new NotSupportedException(
                "The managed method-pointer redirect was retired. Launch Overwolf through the native CLR profiler using the 'instrument' command.");
        }
    }
}
