using System;
using System.Collections.Generic;
using System.Linq;
using Arin.NET.Entities;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using static blog.ArinMiddleware;

namespace blog
{
    public class ArinTelemetry : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor context;

        public ArinTelemetry(IHttpContextAccessor context)
        {
            this.context = context;
        }

        public void Initialize(ITelemetry telemetry)
        {
            var address = GetIpAddress(context.HttpContext);

            telemetry.Context.Location.Ip = address.ToString();

            if (telemetry is ISupportProperties propTelemetry &&
                Cache.TryGetValue(address.ToString(), out AddressCacheItem value) &&
                !propTelemetry.Properties.ContainsKey("org"))
            {
                string orgName = TryGetOrgName(value.Value);

                if (!string.IsNullOrWhiteSpace(orgName))
                {
                    propTelemetry.Properties.Add("org", orgName);
                }

                string domainName = TryGetDomainName(value.Value);

                if (!string.IsNullOrWhiteSpace(domainName))
                {
                    propTelemetry.Properties.Add("networkDomain", domainName);
                }
            }
        }

        private static string TryGetDomainName(IpResponse value)
        {
            if (value == null)
            {
                return null;
            }

            var domains = new List<string>();

            var entities = new List<Entity>();

            entities.AddRange(value.Entities);

            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];

                entities.AddRange(entity.Entities);

                ExtractDomain(domains, entity);
            }

            var common = new string(CommonDomain(domains));

            if (domains.Contains(common))
            {
                return common;
            }

            return domains.FirstOrDefault();
        }

        private static void ExtractDomain(List<string> domains, Entity entity)
        {
            if (entity.VCard != null && entity.VCard.TryGetValue("email", out ContactCardProperty emailProp))
            {
                foreach (var email in emailProp.Value)
                {
                    var indexOf = email.IndexOf('@');

                    if (indexOf >= 0)
                    {
                        var domain = email.Substring(indexOf + 1);

                        domains.Add(domain);
                    }
                }
            }
        }

        private static string CommonDomain(IEnumerable<string> list)
        {
            var reversed = list.Distinct().Select(s => new string(s.Reverse().ToArray()));

            var chars = reversed
                            .First().Substring(0, reversed.Min(s => s.Length))
                            .TakeWhile((c, i) => reversed.All(s => s[i] == c)).ToArray();

            return new string(chars.Reverse().ToArray());
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
