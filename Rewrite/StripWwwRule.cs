using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;

namespace blog.Rewrite
{
    public class StripWwwRule : HostRedirectRule
    {
        protected override bool ShouldRedirect(RewriteContext context, out string host)
        {
            host = null;
            HttpRequest request = context.HttpContext.Request;

            if (request.Host.Value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                host = request.Host.Value[4..];
                return true;
            }

            return false;
        }
    }
}
