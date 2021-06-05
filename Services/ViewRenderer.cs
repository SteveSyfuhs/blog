using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace blog.Services
{
    internal static class ViewRenderer
    {
        public static async Task<string> RenderView<T>(HttpContext httpContext, string viewName, T model)
        {
            var sp = httpContext.RequestServices;

            var viewEngine = sp.GetRequiredService<IRazorViewEngine>();
            var tempDataProvider = sp.GetRequiredService<ITempDataProvider>();

            var view = viewEngine.GetView("~/", viewName, false).View;

            using (var output = new StringWriter())
            {
                var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor());

                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    new ViewDataDictionary<T>(
                        metadataProvider: new EmptyModelMetadataProvider(),
                        modelState: new ModelStateDictionary())
                    {
                        Model = model
                    },
                    new TempDataDictionary(
                        httpContext,
                        tempDataProvider),
                    output,
                    new HtmlHelperOptions());

                await view.RenderAsync(viewContext);

                return output.ToString();
            }
        }
    }
}
