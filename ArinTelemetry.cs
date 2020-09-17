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
                propTelemetry.Properties.Add("org", value.Value?.Organization?.Name?.Value);
                //propTelemetry.Properties.Add("client-ip", clientIPValue);
            }
        }
    }
}
