using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Arin.NET.Client;
using Arin.NET.Entities;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;

namespace blog
{
    internal class ArinMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly TelemetryClient client;

        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);

        internal static readonly ConcurrentDictionary<string, AddressCacheItem> Cache = new ConcurrentDictionary<string, AddressCacheItem>();
        internal static readonly DateTimeOffset Start = DateTimeOffset.UtcNow;

        public ArinMiddleware(RequestDelegate next, TelemetryClient client)
        {
            _next = next;
            this.client = client;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteAddr = context.Connection.RemoteIpAddress;

            try
            {
                await LookupAddress(remoteAddr);
            }
            catch (Exception ex)
            {
                client.TrackException(ex);
            }

            await _next(context);
        }

        private async Task<AddressCacheItem> LookupAddress(IPAddress remoteAddr)
        {
            var value = LookupCache(remoteAddr);

            if (value.Value != null)
            {
                return value;
            }

            if (IPAddress.IsLoopback(remoteAddr) || remoteAddr.IsIPv6LinkLocal || remoteAddr.IsIPv6SiteLocal)
            {
                return default;
            }

            var client = new ArinClient();

            var ipResponse = await client.Query(remoteAddr);

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