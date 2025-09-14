using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CveWebApp.Models;
using CveWebApp.Data;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for user authentication and account management
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        // GET: Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                
                var user = await _userManager.FindByEmailAsync(model.Email);
                var sourceIP = GetClientIpAddress();
                var userAgent = Request.Headers.UserAgent.ToString();

                if (result.Succeeded)
                {
                    // Log successful login
                    await LogLoginAttemptAsync(model.Email, user?.Email, sourceIP, userAgent, true, null);
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    string failureReason = "Invalid credentials";
                    if (result.IsLockedOut)
                        failureReason = "Account locked out";
                    else if (result.RequiresTwoFactor)
                        failureReason = "Two-factor authentication required";
                    else if (result.IsNotAllowed)
                        failureReason = "Account not allowed to sign in";

                    // Log failed login
                    await LogLoginAttemptAsync(model.Email, user?.Email, sourceIP, userAgent, false, failureReason);
                    
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
            }

            return View(model);
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        #region Helpers

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        private string GetClientIpAddress()
        {
            // Check for X-Forwarded-For header first (proxy scenarios)
            var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                // Take the first IP if multiple are present
                return xForwardedFor.Split(',')[0].Trim();
            }

            // Check for X-Real-IP header
            var xRealIp = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            // Fall back to connection remote IP
            return Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private async Task LogLoginAttemptAsync(string username, string? email, string sourceIP, string? userAgent, bool isSuccess, string? failureReason)
        {
            try
            {
                var loginAttempt = new LoginAttempt
                {
                    Timestamp = DateTime.UtcNow,
                    Username = username,
                    Email = email,
                    SourceIP = sourceIP,
                    UserAgent = userAgent?.Length > 256 ? userAgent.Substring(0, 256) : userAgent,
                    IsSuccess = isSuccess,
                    FailureReason = failureReason
                };

                _context.LoginAttempts.Add(loginAttempt);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log the error but don't let it prevent login
                // In production, you might want to use a proper logging framework
                Console.WriteLine($"Failed to log login attempt: {ex.Message}");
            }
        }

        #endregion
    }
}