using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;

namespace blog.Rewrite
{
    public class RedirectAzWebRule : HostRedirectRule
    {
        private readonly string azWebRedirect;

        public RedirectAzWebRule(string azWebRedirect)
        {
            this.azWebRedirect = azWebRedirect;
        }

        protected override bool ShouldRedirect(RewriteContext context, out string host)
        {
            host = null;
            HttpRequest request = context.HttpContext.Request;

            if (request.Host.Value.EndsWith(".azurewebsites.net", StringComparison.OrdinalIgnoreCase))
            {
                host = this.azWebRedirect;
                return true;
            }

            return false;
        }
    }
}
