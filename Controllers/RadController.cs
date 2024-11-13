using blog.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace blog.Controllers
{
    [OutputCache(Duration = 0)]
    public class RadController : Controller
    {
        private readonly IBlogService service;

        public RadController(IBlogService service)
        {
            this.service = service;
        }

        [HttpGet("/rad/{device}/chart/{chart}")]
        public IActionResult Chart(string device, string chart)
        {
            return View("chart", new RadModel { Device = device, Chart = chart });
        }

        [HttpGet("/api/rad/{id}/{sensor?}/{start?}/{stop?}")]
        public async Task<IActionResult> CurrentStats([FromRoute]string id, [FromRoute] string sensor, [FromRoute] int? start, int? stop)
        {
            var url = $"https://data.uradmonitor.com/api/v1/devices/{id}/{sensor}";

            if (start != null)
            {
                url += $"/{start}";
            }

            if (stop != null)
            {
                url += $"/{stop}";
            }

            var result = await Get(url);

            return new ContentResult() { Content = result, ContentType = "application/json" };
        }

        private async Task<string> Get(string url)
        {
            if (!http.Value.DefaultRequestHeaders.Contains("X-User-id"))
            {
                http.Value.DefaultRequestHeaders.Add("X-User-id", service.Settings.URadUserId);
                http.Value.DefaultRequestHeaders.Add("X-User-hash", service.Settings.URadUserKey);
            }

            var result = await http.Value.GetAsync(url);

            return await result.Content.ReadAsStringAsync();
        }

        private static readonly Lazy<HttpClient> http = new Lazy<HttpClient>();
    }
}
