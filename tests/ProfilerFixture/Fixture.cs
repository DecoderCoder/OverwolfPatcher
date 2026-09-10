namespace MetadataNoise
{
    // This getter is intentionally unrelated to subscription identity. A global
    // metadata search must not select it instead of the getter used by the target body.
    public sealed class UnrelatedIdentity
    {
        public string UID { get; set; }
    }
}

namespace ODKv2API
{
    public enum SubscriptionState { Active = 3, Expired = 4 }

    public sealed class DetailedActivePlan
    {
        public int PlanId { get; set; }
        public SubscriptionState State { get; set; }
        public long ExpiryDate { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public int PeriodMonths { get; set; }
    }
}

namespace OverWolf.Client.Core.ODKv2.Profile
{
    public sealed class OverwolfSubscription : SubscriptionIdentity
    {
        public ODKv2API.DetailedActivePlan[] GetExtensionSubscriptions()
        {
            if (UID == "unused") return new ODKv2API.DetailedActivePlan[0];
            return new[] { new ODKv2API.DetailedActivePlan { PlanId = 7 } };
        }

        public int[] GetExtensionSubscriptionsIds()
        {
            return new[] { 7 };
        }
    }

    public class SubscriptionIdentity
    {
        public string UID { get; set; }
    }
}
