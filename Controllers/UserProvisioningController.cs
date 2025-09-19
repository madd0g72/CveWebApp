using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CveWebApp.Models;
using CveWebApp.Services;

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
        private readonly IActiveDirectoryService _activeDirectoryService;
        private readonly ActiveDirectorySettings _adSettings;

        public UserProvisioningController(
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            IActiveDirectoryService activeDirectoryService,
            IOptions<ActiveDirectorySettings> adSettings)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _activeDirectoryService = activeDirectoryService;
            _adSettings = adSettings.Value;
        }

        // GET: UserProvisioning
        public async Task<IActionResult> Index()
        {
            var model = new UserProvisioningViewModel();
            
            // Load existing users
            await LoadExistingUsersAsync(model);
            
            // Load AD group members if AD is configured
            await LoadAdGroupMembersAsync(model);
            
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
                    await LoadExistingUsersAsync(model);
                    return View(model);
                }

                // Check if username already exists
                var existingUsername = await _userManager.FindByNameAsync(model.Username);
                if (existingUsername != null)
                {
                    ModelState.AddModelError(string.Empty, "A user with this username already exists.");
                    await LoadExistingUsersAsync(model);
                    return View(model);
                }

                // Verify that the selected role exists
                if (!await _roleManager.RoleExistsAsync(model.SelectedRole))
                {
                    ModelState.AddModelError(string.Empty, "Selected role does not exist.");
                    await LoadExistingUsersAsync(model);
                    return View(model);
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = model.Username,  // Keep separate username for flexibility
                    Email = model.Email,
                    EmailConfirmed = true,
                    FullName = model.FullName,
                    CreatedAt = DateTime.UtcNow
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

            await LoadExistingUsersAsync(model);
            return View(model);
        }

        // GET: UserProvisioning/ManageRoles/{id}
        public async Task<IActionResult> ManageRoles(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

            var model = new UserRoleManagementViewModel
            {
                UserId = user.Id,
                Username = user.UserName!,
                CurrentRoles = userRoles.ToList(),
                AvailableRoles = allRoles,
                SelectedRoles = userRoles.ToList()
            };

            return PartialView("_ManageRolesModal", model);
        }

        // POST: UserProvisioning/UpdateRoles
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(UserRoleManagementViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Prevent admin from removing their own admin role
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId && !model.SelectedRoles.Contains("Admin"))
            {
                return Json(new { success = false, message = "You cannot remove your own Admin role." });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            
            // Remove user from all current roles
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return Json(new { success = false, message = "Failed to update user roles." });
            }

            // Add user to selected roles
            if (model.SelectedRoles.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                if (!addResult.Succeeded)
                {
                    return Json(new { success = false, message = "Failed to update user roles." });
                }
            }

            return Json(new { success = true, message = $"Roles updated successfully for {user.UserName}." });
        }

        // POST: UserProvisioning/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Prevent admin from deleting their own account
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                return Json(new { success = false, message = "You cannot delete your own account." });
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                return Json(new { success = true, message = $"User '{user.UserName}' has been deleted successfully." });
            }

            return Json(new { success = false, message = "Failed to delete user." });
        }

        private async Task LoadExistingUsersAsync(UserProvisioningViewModel model)
        {
            var users = await _userManager.Users.ToListAsync();
            var currentUserId = _userManager.GetUserId(User);

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                model.ExistingUsers.Add(new UserManagementItem
                {
                    Id = user.Id,
                    Username = user.UserName!,
                    Email = user.Email!,
                    FullName = user.FullName ?? "",
                    Roles = roles.ToList(),
                    CreatedAt = user.CreatedAt,
                    IsCurrentUser = user.Id == currentUserId
                });
            }

            // Sort by creation date, newest first
            model.ExistingUsers = model.ExistingUsers.OrderByDescending(u => u.CreatedAt).ToList();
        }

        private async Task LoadAdGroupMembersAsync(UserProvisioningViewModel model)
        {
            // Check if AD is configured
            model.IsAdConfigured = _adSettings.Enabled && _activeDirectoryService.IsConfigured;
            
            if (!model.IsAdConfigured)
            {
                return;
            }

            try
            {
                // Set group names for display
                model.AdAdminGroupName = ExtractGroupName(_adSettings.AdminGroupDn);
                model.AdUserGroupName = ExtractGroupName(_adSettings.UserGroupDn);

                // Load admin group members
                if (!string.IsNullOrEmpty(_adSettings.AdminGroupDn))
                {
                    var adminUsers = await _activeDirectoryService.GetUsersByGroupMembershipAsync(_adSettings.AdminGroupDn);
                    foreach (var adUser in adminUsers)
                    {
                        var localUser = await _userManager.FindByNameAsync(adUser.Username);
                        var displayInfo = new AdUserDisplayInfo
                        {
                            Username = adUser.Username,
                            DisplayName = adUser.DisplayName,
                            Email = adUser.Email,
                            IsProvisioned = localUser != null,
                            LocalRoles = localUser != null ? string.Join(", ", await _userManager.GetRolesAsync(localUser)) : "Not provisioned"
                        };
                        model.AdAdminUsers.Add(displayInfo);
                    }
                }

                // Load user group members
                if (!string.IsNullOrEmpty(_adSettings.UserGroupDn))
                {
                    var userGroupUsers = await _activeDirectoryService.GetUsersByGroupMembershipAsync(_adSettings.UserGroupDn);
                    foreach (var adUser in userGroupUsers)
                    {
                        var localUser = await _userManager.FindByNameAsync(adUser.Username);
                        var displayInfo = new AdUserDisplayInfo
                        {
                            Username = adUser.Username,
                            DisplayName = adUser.DisplayName,
                            Email = adUser.Email,
                            IsProvisioned = localUser != null,
                            LocalRoles = localUser != null ? string.Join(", ", await _userManager.GetRolesAsync(localUser)) : "Not provisioned"
                        };
                        model.AdUserGroupUsers.Add(displayInfo);
                    }
                }
            }
            catch (Exception)
            {
                // Log error but don't fail the page load
                // The users can still manage local users even if AD group lookup fails
            }
        }

        private static string? ExtractGroupName(string? groupDn)
        {
            if (string.IsNullOrEmpty(groupDn))
                return null;

            // Extract group name from DN (e.g., "CN=CVE-Admins,CN=Users,DC=company,DC=local" -> "CVE-Admins")
            var cnIndex = groupDn.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
            if (cnIndex >= 0)
            {
                var start = cnIndex + 3; // Skip "CN="
                var commaIndex = groupDn.IndexOf(',', start);
                if (commaIndex > start)
                {
                    return groupDn.Substring(start, commaIndex - start);
                }
                else
                {
                    // No comma found, take the rest
                    return groupDn.Substring(start);
                }
            }

            return groupDn; // Fallback to full DN if parsing fails
        }
    }
}