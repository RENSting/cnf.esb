using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using cnf.esb.web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace cnf.esb.web.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        readonly AdminSettings _admin;
        public HomeController(IOptionsSnapshot<AdminSettings> adminSettings)
        {
            _admin = adminSettings.Value;
        }
        public IActionResult Login()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel viewModel)
        {
            if(viewModel.UserName == _admin.UserName && viewModel.Password == _admin.Password)
            {
                var claims = new List<Claim>
                {
                    new Claim("userid", viewModel.UserName)
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var properties = new AuthenticationProperties
                {
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20),
                };
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
                return RedirectToAction(nameof(Index));
            }
            else
            {
                ViewData["ErrorMessage"] = "用户名或者口令错误";
                return View(viewModel);
            }
        }

        public IActionResult Logout()
        {
            HttpContext.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
