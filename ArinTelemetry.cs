using System.Linq;
using Arin.NET.Entities;
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
                string orgName = TryGetOrgName(value.Value);

                if (!string.IsNullOrWhiteSpace(orgName))
                {
                    propTelemetry.Properties.Add("org", orgName);
                }
            }
        }

        private static string TryGetOrgName(IpResponse value)
        {
            if (value == null)
            {
                return null;
            }

            string name = null;

            foreach (var entity in value.Entities)
            {
                if (entity.VCard != null && entity.VCard.TryGetValue("fn", out ContactCardProperty prop))
                {
                    name = prop.Value.FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        break;
                    }
                }
            }

            return name;
        }
    }
}
