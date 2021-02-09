using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace blog
{
    public class WebMarkupMinFileNotFoundHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public WebMarkupMinFileNotFoundHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (FileNotFoundException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
    }
}
