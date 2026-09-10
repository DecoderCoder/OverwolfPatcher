using System;
using System.Linq;
using OverWolf.Client.Core.ODKv2.Profile;

internal static class Program
{
    const string App = "cghphpbjeabdkomiphingnegihoigeggcfphdofo";
    const string OtherApp = "pppppppppppppppppppppppppppppppppppppppp";

    static int Main()
    {
        var instance = new OverwolfSubscription { UID = App };
        var detailed = instance.GetExtensionSubscriptions();
        var ids = instance.GetExtensionSubscriptionsIds();
        if (detailed.Length != 1 || detailed[0].PlanId != 61 ||
            detailed[0].State != ODKv2API.SubscriptionState.Active ||
            ids.SequenceEqual(new[] { 61 }) == false)
        {
            Console.Error.WriteLine("Profiler replacement did not execute.");
            return 1;
        }
        if (detailed[0].ExpiryDate <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            Console.Error.WriteLine("Profiler replacement returned an expired plan.");
            return 1;
        }
        var otherInstance = new OverwolfSubscription { UID = OtherApp };
        var otherDetailed = otherInstance.GetExtensionSubscriptions();
        var otherIds = otherInstance.GetExtensionSubscriptionsIds();
        if (otherDetailed.Length != 1 || otherDetailed[0].PlanId != 61 ||
            otherDetailed[0].State != ODKv2API.SubscriptionState.Active ||
            otherIds.SequenceEqual(new[] { 61 }) == false)
        {
            Console.Error.WriteLine("Profiler replacement did not execute for the second configured extension.");
            return 1;
        }
        Console.WriteLine("PASS: x64 startup profiler replaced both fixture methods before JIT.");
        return 0;
    }
}
