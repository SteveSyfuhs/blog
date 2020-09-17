using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using static blog.ArinMiddleware;

namespace blog
{
    public class ArinTelemetry : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry?.Context?.Location?.Ip == null)
            {
                return;
            }

            if (telemetry is ISupportProperties propTelemetry &&
                Cache.TryGetValue(telemetry.Context.Location.Ip, out AddressCacheItem value) &&
                !propTelemetry.Properties.ContainsKey("org"))
            {
                var orgName = value.Value?.Organization?.Name?.Value ??
                              value.Value?.Network?.OrgRef?.Name;

                if (!string.IsNullOrWhiteSpace(orgName))
                {
                    propTelemetry.Properties.Add("org", orgName);
                }
            }
        }
    }
}
