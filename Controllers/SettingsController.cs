using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace blog.Controllers
{
    [Authorize, AutoValidateAntiforgeryToken]
    public class SettingsController : Controller
    {
        private readonly IBlogService _blog;

        public SettingsController(IBlogService blog)
        {
            _blog = blog;
        }

        [Route("/manage/settings")]
        [HttpGet]
        public IActionResult EditSettings()
        {
            return View("Settings", _blog.Settings);
        }

        [Route("/manage/settings")]
        [HttpPost]
        public async Task<IActionResult> UpdateSettings(BlogSettings settings)
        {
            if (!ModelState.IsValid)
            {
                return View("Settings", _blog.Settings);
            }

            AddUrlReplacements(settings);

            AddRedirects(settings);

            await _blog.UpdateSettings(settings);

            return View("Settings", _blog.Settings);
        }

        private void AddRedirects(BlogSettings settings)
        {
            var find = Request.Form["AddRedirectFind"].ToString();
            var replace = Request.Form["AddRedirectReplace"].ToString();

            if (!string.IsNullOrWhiteSpace(find) && !string.IsNullOrWhiteSpace(replace))
            {
                settings.Redirects.Add(new UrlReplacement { Find = find, Replace = replace });
            }

            foreach (var redirect in settings.Redirects.ToArray())
            {
                if (string.IsNullOrWhiteSpace(redirect.Find))
                {
                    settings.Redirects.Remove(redirect);
                }
            }
        }

        private void AddUrlReplacements(BlogSettings settings)
        {
            var find = Request.Form["AddUrlFind"].ToString();
            var replace = Request.Form["AddUrlReplace"].ToString();

            if (!string.IsNullOrWhiteSpace(find) && !string.IsNullOrWhiteSpace(replace))
            {
                settings.UrlReplacements.Add(new UrlReplacement { Find = find, Replace = replace });
            }

            foreach (var replacement in settings.UrlReplacements.ToArray())
            {
                if (string.IsNullOrWhiteSpace(replacement.Find))
                {
                    settings.UrlReplacements.Remove(replacement);
                }
            }
        }
    }
}
