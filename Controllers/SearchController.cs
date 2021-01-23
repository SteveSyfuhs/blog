using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Threading.Tasks;

namespace blog
{
    public class SearchController : Controller
    {
        private readonly IBlogService _blog;
        private readonly IOptionsSnapshot<BlogSettings> _settings;

        public SearchController(IBlogService blog, IOptionsSnapshot<BlogSettings> settings)
        {
            _blog = blog;
            _settings = settings;
        }

        [Route("/search")]
        public async Task<IActionResult> Index([FromQuery]string q, [FromQuery]int page)
        {
            ViewData["Title"] = _settings.Value.Name + " | " + _settings.Value.Description;

            if (string.IsNullOrWhiteSpace(q))
            {
                return View();
            }

            ViewData["Title"] = q + " | " + ViewData["Title"];

            if (page <= 0)
            {
                page = 0;
            }

            var pageResults = _settings.Value.PostsPerPage * 3;

            var skip = page * pageResults;

            var results = await _blog.Search(q, skip, pageResults);

            results.Page = page;

            if (skip + results.Posts.Count() < results.Results)
            {
                ViewData["prev"] = $"/search?q={q}&page={page + 1}";
            }

            ViewData["next"] = $"/search?q={q}&page={page - 1}";

            return View(results);
        }
    }
}