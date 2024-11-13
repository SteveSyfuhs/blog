using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using System.Threading.Tasks;
using System.Threading;

namespace blog
{
    public class OutputCacheAuthenticatedPolicy : IOutputCachePolicy
    {
        public static readonly OutputCacheAuthenticatedPolicy Instance = new();

        private OutputCacheAuthenticatedPolicy() { }

        ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
        {
            var attemptOutputCaching = AttemptOutputCaching(context);

            context.EnableOutputCaching = true;
            context.AllowCacheLookup = attemptOutputCaching;
            context.AllowCacheStorage = attemptOutputCaching;
            context.AllowLocking = true;

            context.CacheVaryByRules.QueryKeys = "*";

            return ValueTask.CompletedTask;
        }

        private static bool AttemptOutputCaching(OutputCacheContext context)
        {
            var request = context.HttpContext.Request;

            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                return false;
            }

            return true;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;
    }
}