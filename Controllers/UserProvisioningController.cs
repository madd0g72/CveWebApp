using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CveWebApp.Models;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for user provisioning functionality - restricted to Admin role only
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class UserProvisioningController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserProvisioningController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: UserProvisioning
        public IActionResult Index()
        {
            var model = new UserProvisioningViewModel();
            return View(model);
        }

        // POST: UserProvisioning
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(UserProvisioningViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "A user with this email address already exists.");
                    return View(model);
                }

                // Check if username already exists
                var existingUsername = await _userManager.FindByNameAsync(model.Username);
                if (existingUsername != null)
                {
                    ModelState.AddModelError(string.Empty, "A user with this username already exists.");
                    return View(model);
                }

                // Verify that the selected role exists
                if (!await _roleManager.RoleExistsAsync(model.SelectedRole))
                {
                    ModelState.AddModelError(string.Empty, "Selected role does not exist.");
                    return View(model);
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    Email = model.Email,
                    EmailConfirmed = true,
                    FullName = model.FullName
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                
                if (result.Succeeded)
                {
                    // Assign role to user
                    await _userManager.AddToRoleAsync(user, model.SelectedRole);
                    
                    TempData["SuccessMessage"] = $"User '{model.Username}' has been successfully created with the '{model.SelectedRole}' role.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            return View(model);
        }
    }
}