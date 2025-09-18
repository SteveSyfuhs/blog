using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Arin.NET.Client;
using Arin.NET.Entities;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace blog
{
    internal class ArinMiddleware(RequestDelegate next, TelemetryClient client, IConfiguration config)
    {
        private static readonly ArinClient arinClient = new();

        private readonly RequestDelegate _next = next;
        private readonly TelemetryClient client = client;

        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);

        internal static readonly ConcurrentDictionary<string, AddressCacheItem> Cache = new();
        internal static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;

        public async Task InvokeAsync(HttpContext context)
        {
            Task lookupTask = Task.CompletedTask;

            var address = GetIpAddress(context, config);

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                try
                {
                    lookupTask = LookupAddress(address, cts.Token);
                }
                catch (Exception ex)
                {
                    client.TrackException(ex);
                }

                await _next(context);

                try
                {
                    await lookupTask;
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    client.TrackException(ex);
                }
            }
        }

        public static IPAddress GetIpAddress(HttpContext context, IConfiguration config)
        {
            if (context == null)
            {
                return null;
            }

            var request = context.Request;

            var cfAddr = request.Headers["CF-CONNECTING-IP"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(cfAddr) && IPAddress.TryParse(cfAddr, out IPAddress addr))
            {
                return addr;
            }

            var ipAddress = context.GetServerVariable("HTTP_X_FORWARDED_FOR");

            if (!string.IsNullOrEmpty(ipAddress))
            {
                var addresses = ipAddress.Split(',', StringSplitOptions.RemoveEmptyEntries);

                if (addresses.Length != 0 && IPAddress.TryParse(addresses[0], out addr))
                {
                    return addr;
                }
            }

            if (IsLoopback(context?.Connection?.RemoteIpAddress))
            {
                var fake = config?.GetValue<string>("fakeip");

                if (!string.IsNullOrWhiteSpace(fake))
                {
                    return IPAddress.Parse(fake);
                }
            }

            return context.Connection.RemoteIpAddress;
        }

        private static bool IsLoopback(IPAddress address)
        {
            if (address == null)
            {
                return true;
            }

            return IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        private async Task<AddressCacheItem> LookupAddress(IPAddress remoteAddr, CancellationToken cancellation)
        {
            var value = LookupCache(remoteAddr);

            if (value.Value != null)
            {
                return value;
            }

            if (IsLoopback(remoteAddr))
            {
                return default;
            }

            var ipResponse = await arinClient.Query(remoteAddr, cancellation);

            if (ipResponse is IpResponse response)
            {
                return CacheResult(remoteAddr, response);
            }
            else if (ipResponse is ErrorResponse error)
            {
                this.client.TrackException(new HttpRequestException(error?.Title ?? "Unknown HTTP error"));
                return default;
            }

            return default;
        }

        private static AddressCacheItem CacheResult(IPAddress remoteAddr, IpResponse ipResponse)
        {
            var value = new AddressCacheItem
            {
                Value = ipResponse,
                Created = DateTimeOffset.UtcNow
            };

            Cache[remoteAddr.ToString()] = value;

            return value;
        }

        private static AddressCacheItem LookupCache(IPAddress remoteAddr)
        {
            if (remoteAddr == null)
            {
                return default;
            }

            var key = remoteAddr.ToString();

            if (!ExpireOrGet(key, out AddressCacheItem value))
            {
                return default;
            }

            return value;
        }

        private static bool ExpireOrGet(string key, out AddressCacheItem value)
        {
            if (!Cache.TryGetValue(key, out value))
            {
                return false;
            }

            if (PurgeIfExpired(key, value))
            {
                return false;
            }

            return true;
        }

        private static bool PurgeIfExpired(string key, AddressCacheItem value)
        {
            if (value.Created.Add(Lifetime) < DateTimeOffset.UtcNow)
            {
                Cache.TryRemove(key.ToString(), out _);

                return true;
            }

            return false;
        }

        internal struct AddressCacheItem
        {
            public IpResponse Value { get; set; }

            public DateTimeOffset Created { get; set; }
        }
    }
}