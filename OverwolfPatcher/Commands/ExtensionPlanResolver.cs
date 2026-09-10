using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OverwolfPatcher.Testing
{
    internal sealed class ExtensionPlanSelection
    {
        internal ExtensionPlanSelection(string extensionId, IEnumerable<int> plans, string source)
        {
            ExtensionId = extensionId;
            Plans = plans.ToArray();
            Source = source;
        }

        internal string ExtensionId { get; private set; }
        internal int[] Plans { get; private set; }
        internal string Source { get; private set; }
    }

    internal sealed class ExtensionPlanSkip
    {
        internal ExtensionPlanSkip(string extensionId, string reason)
        {
            ExtensionId = extensionId;
            Reason = reason;
        }

        internal string ExtensionId { get; private set; }
        internal string Reason { get; private set; }
    }

    internal sealed class ExtensionPlanResolution
    {
        internal ExtensionPlanResolution(IEnumerable<ExtensionPlanSelection> selections,
            IEnumerable<ExtensionPlanSkip> skipped)
        {
            Selections = selections.ToArray();
            Skipped = skipped.ToArray();
        }

        internal ExtensionPlanSelection[] Selections { get; private set; }
        internal ExtensionPlanSkip[] Skipped { get; private set; }

        internal string ToEnvironmentValue()
        {
            return string.Join(";", Selections.Select(selection =>
                selection.ExtensionId + "=" + string.Join(",", selection.Plans)));
        }
    }

    // Subscription plan IDs are assigned to an app's subscription catalog by
    // Overwolf/the app developer. They are not implied by an extension UID,
    // manifest permission, or the presence of the Core API. The installed
    // Overwolf Appstore uses this UID-scoped catalog for its legacy purchase UI,
    // making it the authoritative generic source available to the launcher.
    internal static class ExtensionPlanResolver
    {
        internal const string CatalogUrl = "https://console-api.overwolf.com/v2/subscription-plans/app/";

        internal static ExtensionPlanSelection ResolveSingle(string extensionId,
            Func<string, int[]> catalogLookup = null)
        {
            var id = NormalizeExtensionId(extensionId);
            var plans = ValidatePlanIds((catalogLookup ?? FetchCatalogPlanIds)(id));
            if (plans.Length == 0)
                throw new ArgumentException("The legacy Overwolf catalog returned no plan records for extension " + id + ". The extension is not supported by the automatic in-memory patch and was left unchanged.");
            return new ExtensionPlanSelection(id, plans, "Overwolf subscription catalog");
        }

        internal static ExtensionPlanResolution ResolveAll(IEnumerable<string> extensionIds,
            Func<string, int[]> catalogLookup = null)
        {
            if (extensionIds == null) throw new ArgumentNullException("extensionIds");
            var installed = extensionIds.Select(NormalizeExtensionId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var selections = new ExtensionPlanSelection[installed.Length];
            var skipped = new ExtensionPlanSkip[installed.Length];
            var lookup = catalogLookup ?? FetchCatalogPlanIds;

            Parallel.For(0, installed.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index =>
            {
                var id = installed[index];
                try
                {
                    var plans = ValidatePlanIds(lookup(id));
                    if (plans.Length > 0)
                        selections[index] = new ExtensionPlanSelection(id, plans, "Overwolf subscription catalog");
                    else
                        skipped[index] = new ExtensionPlanSkip(id,
                            "legacy Overwolf catalog has no plan metadata; original methods retained");
                }
                catch (Exception error)
                {
                    skipped[index] = new ExtensionPlanSkip(id,
                        "Overwolf catalog lookup failed (" + error.Message + "); original methods retained");
                }
            });
            return new ExtensionPlanResolution(selections.Where(value => value != null),
                skipped.Where(value => value != null));
        }

        internal static int[] FetchCatalogPlanIds(string extensionId)
        {
            var request = (HttpWebRequest)WebRequest.Create(CatalogUrl + Uri.EscapeDataString(extensionId));
            request.Method = "GET";
            request.Accept = "application/json";
            request.UserAgent = "OverwolfPatcher/plan-resolution";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new System.IO.StreamReader(stream))
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new InvalidOperationException("HTTP " + (int)response.StatusCode);
                return ParseCatalogPlanIds(reader.ReadToEnd());
            }
        }

        internal static int[] ParseCatalogPlanIds(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new int[0];
            object value = new JavaScriptSerializer().DeserializeObject(json);
            var items = value as object[];
            if (items == null)
                throw new InvalidOperationException("Overwolf catalog response was not an array.");

            var plans = new List<int>();
            foreach (var item in items)
            {
                var record = item as IDictionary<string, object>;
                if (record == null)
                    throw new InvalidOperationException("Overwolf catalog contained a non-object plan record.");
                object id;
                if (!record.TryGetValue("id", out id))
                    throw new InvalidOperationException("Overwolf catalog plan record did not contain an id.");
                int plan;
                if (!TryReadPlanId(id, out plan))
                    throw new InvalidOperationException("Overwolf catalog plan id was not a positive integer.");
                if (plans.Contains(plan)) continue;
                plans.Add(plan);
            }
            if (plans.Count > 32) throw new InvalidOperationException("Overwolf catalog returned more than 32 plans.");
            return plans.ToArray();
        }

        static bool TryReadPlanId(object value, out int plan)
        {
            plan = 0;
            if (value is int integer) plan = integer;
            else if (value is long longValue && longValue <= int.MaxValue) plan = (int)longValue;
            else if (value is decimal decimalValue && decimal.Truncate(decimalValue) == decimalValue &&
                decimalValue <= int.MaxValue) plan = (int)decimalValue;
            else return false;
            return plan > 0;
        }

        static int[] ValidatePlanIds(IEnumerable<int> values)
        {
            var plans = (values ?? Enumerable.Empty<int>()).Distinct().ToArray();
            if (plans.Length > 32 || plans.Any(value => value <= 0))
                throw new InvalidOperationException("Overwolf catalog returned an invalid plan ID set.");
            return plans;
        }

        static string NormalizeExtensionId(string value)
        {
            var id = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (id.Length != 40 || id.Any(character => character < 'a' || character > 'p'))
                throw new ArgumentException("Expected a 40-character Overwolf extension ID.");
            return id;
        }
    }
}
