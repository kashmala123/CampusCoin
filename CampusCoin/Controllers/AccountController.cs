using Microsoft.AspNetCore.Mvc;

namespace CampusCoin.Controllers
{
    /// <summary>
    /// Frontend Login / Register pages.
    /// Actual authentication is handled by JS → /Auth/Login and /Auth/Register.
    /// </summary>
    public class AccountController : Controller
    {
        [HttpGet]
        [HttpPost] // prevents 405 if form ever posts here
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                return role == "Admin"
                    ? RedirectToAction("Index", "Admin")
                    : RedirectToAction("Index", "Dashboard");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpGet]
        [HttpPost]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpGet]
        [HttpPost]
        public IActionResult ForgotPassword()
        {
            return View();
        }
    }
}
