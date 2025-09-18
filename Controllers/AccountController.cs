using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CveWebApp.Models;
using CveWebApp.Data;
using CveWebApp.Services;
using Microsoft.Extensions.Options;

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
        private readonly IFileLoggingService _fileLoggingService;
        private readonly IWebHostEnvironment _environment;
        private readonly IActiveDirectoryService _activeDirectoryService;
        private readonly IUserProvisioningService _userProvisioningService;
        private readonly ActiveDirectorySettings _adSettings;

        public AccountController(
            UserManager<ApplicationUser> userManager, 
            SignInManager<ApplicationUser> signInManager, 
            ApplicationDbContext context, 
            IFileLoggingService fileLoggingService, 
            IWebHostEnvironment environment,
            IActiveDirectoryService activeDirectoryService,
            IUserProvisioningService userProvisioningService,
            IOptions<ActiveDirectorySettings> adSettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _fileLoggingService = fileLoggingService;
            _environment = environment;
            _activeDirectoryService = activeDirectoryService;
            _userProvisioningService = userProvisioningService;
            _adSettings = adSettings.Value;
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
                var sourceIP = GetClientIpAddress();
                var userAgent = Request.Headers.UserAgent.ToString();
                ApplicationUser? user = null;
                Microsoft.AspNetCore.Identity.SignInResult result = Microsoft.AspNetCore.Identity.SignInResult.Failed;
                string? authenticationMethod = null;

                // Try Active Directory authentication first if enabled
                if (_adSettings.Enabled && _activeDirectoryService.IsConfigured)
                {
                    var adResult = await _activeDirectoryService.AuthenticateUserAsync(model.Email, model.Password);
                    
                    if (adResult.IsSuccessful)
                    {
                        // Provision or update the AD user in local database
                        user = await _userProvisioningService.ProvisionAdUserAsync(adResult, model.Email);
                        
                        if (user != null)
                        {
                            // Sign in the user without password validation (since AD already validated)
                            await _signInManager.SignInAsync(user, model.RememberMe);
                            result = Microsoft.AspNetCore.Identity.SignInResult.Success;
                            authenticationMethod = "Active Directory";
                            
                            // Log successful AD login
                            await LogLoginAttemptAsync(model.Email, user.Email, sourceIP, userAgent, true, null);
                            
                            await _fileLoggingService.LogActionAsync(
                                "User Login (AD)", 
                                user.Email ?? model.Email, 
                                $"Successful AD authentication from {sourceIP}", 
                                sourceIP);
                            
                            return RedirectToLocal(returnUrl);
                        }
                    }
                }

                // If AD authentication failed or is disabled, try local authentication (if allowed)
                if (!result.Succeeded && (_adSettings.AllowLocalUserFallback || !_adSettings.Enabled))
                {
                    // First try to login with the provided value as username
                    result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                    
                    // If that fails and the input looks like an email, try to find user by email and use their username
                    if (!result.Succeeded && model.Email.Contains("@"))
                    {
                        var userByEmail = await _userManager.FindByEmailAsync(model.Email);
                        if (userByEmail != null && !userByEmail.IsActiveDirectoryUser)
                        {
                            result = await _signInManager.PasswordSignInAsync(userByEmail.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
                            user = userByEmail;
                        }
                    }
                    
                    if (user == null)
                    {
                        user = await _userManager.FindByEmailAsync(model.Email);
                    }
                    
                    authenticationMethod = "Local Database";
                }

                if (result.Succeeded && user != null)
                {
                    // Log successful login
                    await LogLoginAttemptAsync(model.Email, user.Email, sourceIP, userAgent, true, null);
                    
                    await _fileLoggingService.LogActionAsync(
                        $"User Login ({authenticationMethod})", 
                        user.Email ?? model.Email, 
                        $"Successful {authenticationMethod?.ToLower()} authentication from {sourceIP}", 
                        sourceIP);
                    
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
                    
                    await _fileLoggingService.LogActionAsync(
                        "Failed Login Attempt", 
                        user?.Email ?? model.Email, 
                        $"Failed login from {sourceIP}: {failureReason}", 
                        sourceIP);
                    
                    ModelState.AddModelError(string.Empty, _adSettings.Enabled && _activeDirectoryService.IsConfigured 
                        ? "Invalid login attempt. Please check your domain credentials." 
                        : "Invalid login attempt.");
                }
            }

            return View(model);
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var currentUser = User.Identity?.Name ?? "Anonymous";
            var sourceIP = GetClientIpAddress();
            
            await _signInManager.SignOutAsync();
            
            // Log logout action
            await _fileLoggingService.LogActionAsync(
                "User Logout", 
                currentUser, 
                $"User logged out from {sourceIP}", 
                sourceIP);
            
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/AccessDenied
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: Account/ForgotPassword
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return RedirectToAction(nameof(ForgotPasswordConfirmation));
                }

                // Check if this is an Active Directory user
                if (user.IsActiveDirectoryUser)
                {
                    // AD users cannot reset passwords through the application
                    ModelState.AddModelError(string.Empty, 
                        "Password reset is not available for Active Directory users. Please contact your system administrator or use your organization's password reset process.");
                    return View(model);
                }

                // Generate password reset token for local users only
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                
                if (_environment.IsDevelopment())
                {
                    // In development, display the reset token directly
                    TempData["ResetToken"] = code;
                    TempData["ResetEmail"] = model.Email;
                }
                else
                {
                    // In production, provide immediate reset access without email
                    TempData["ResetToken"] = code;
                    TempData["ResetEmail"] = model.Email;
                    TempData["ProductionMode"] = true;
                }
                
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            return View(model);
        }

        // GET: Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // GET: Account/ResetPassword
        [AllowAnonymous]
        public IActionResult ResetPassword(string? code = null, string? email = null)
        {
            if (code == null)
            {
                return BadRequest("A code must be supplied for password reset.");
            }
            
            var model = new ResetPasswordViewModel
            {
                Code = code,
                Email = email ?? string.Empty
            };
            
            return View(model);
        }

        // POST: Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            // Check if this is an Active Directory user
            if (user.IsActiveDirectoryUser)
            {
                // AD users cannot reset passwords through the application
                ModelState.AddModelError(string.Empty, 
                    "Password reset is not available for Active Directory users. Please contact your system administrator.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: Account/ResetPasswordConfirmation
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
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
                // Log the error to file logging system and console
                await _fileLoggingService.LogErrorAsync(
                    $"Failed to log login attempt to database: {ex.Message}", 
                    username, 
                    sourceIP, 
                    ex);
                    
                Console.WriteLine($"Failed to log login attempt: {ex.Message}");
            }
        }

        #endregion
    }
}