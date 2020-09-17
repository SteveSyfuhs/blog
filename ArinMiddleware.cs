using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using ArinWhois.Client;
using ArinWhois.Model;
using Microsoft.AspNetCore.Http;

namespace blog
{
    internal class ArinMiddleware
    {
        private readonly RequestDelegate _next;

        internal static readonly ConcurrentDictionary<string, AddressCacheItem> Cache = new ConcurrentDictionary<string, AddressCacheItem>();
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(3);

        public ArinMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteAddr = context.Connection.RemoteIpAddress;

            await LookupAddress(remoteAddr);

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

            var ipResponse = await client.QueryIpAsync(remoteAddr);

            if (ipResponse == null)
            {
                return default;
            }

           return CacheResult(remoteAddr, ipResponse);
        }

        private static AddressCacheItem CacheResult(IPAddress remoteAddr, Response ipResponse)
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
            public Response Value { get; set; }

            public DateTimeOffset Created { get; set; }
        }
    }
}