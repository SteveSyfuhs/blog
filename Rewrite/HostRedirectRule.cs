using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Net.Http.Headers;

namespace blog.Rewrite
{
    public abstract class HostRedirectRule : IRule
    {
        protected abstract bool ShouldRedirect(RewriteContext context, out string host);

        public void ApplyRule(RewriteContext context)
        {
            HttpRequest request = context.HttpContext.Request;

            if (this.ShouldRedirect(context, out string host))
            {
                string redirectUrl = $"{request.Scheme}://{host}{request.Path}{request.QueryString}";

                context.HttpContext.Response.Headers[HeaderNames.Location] = redirectUrl;
                context.HttpContext.Response.StatusCode = StatusCodes.Status302Found;

                context.Result = RuleResult.EndResponse;
            }
        }
    }
}