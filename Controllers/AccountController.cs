using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace blog.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        [HttpGet]
        [Authorize]
        [Route("/login")]
        public IActionResult Login(string returnUrl = null)
        {
            return LocalRedirect(returnUrl ?? "/");
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("/account/status")]
        public IActionResult Status()
        {
            return View();
        }

        [Route("/logout")]
        public async Task<IActionResult> LogOutAsync()
        {
            await HttpContext.SignOutAsync();

            return LocalRedirect("/");
        }
    }
}
