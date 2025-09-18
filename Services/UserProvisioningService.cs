using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using CveWebApp.Models;
using CveWebApp.Data;

namespace CveWebApp.Services
{
    /// <summary>
    /// Service for provisioning Active Directory users in the local database
    /// </summary>
    public interface IUserProvisioningService
    {
        Task<ApplicationUser?> ProvisionAdUserAsync(AdAuthenticationResult adResult, string username);
        Task<string[]> DetermineUserRolesAsync(AdAuthenticationResult adResult);
    }

    /// <summary>
    /// Implementation of user provisioning service for AD users
    /// </summary>
    public class UserProvisioningService : IUserProvisioningService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ActiveDirectorySettings _adSettings;
        private readonly ILogger<UserProvisioningService> _logger;
        private readonly IActiveDirectoryLoggingService _adLoggingService;

        public UserProvisioningService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<ActiveDirectorySettings> adSettings,
            ILogger<UserProvisioningService> logger,
            IActiveDirectoryLoggingService adLoggingService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _adSettings = adSettings.Value;
            _logger = logger;
            _adLoggingService = adLoggingService;
        }

        /// <summary>
        /// Provisions an AD user in the local database or updates existing user
        /// </summary>
        public async Task<ApplicationUser?> ProvisionAdUserAsync(AdAuthenticationResult adResult, string username)
        {
            if (!adResult.IsSuccessful || string.IsNullOrEmpty(adResult.DistinguishedName))
            {
                return null;
            }

            try
            {
                // Check if user already exists by username
                var existingUser = await _userManager.FindByNameAsync(username);
                
                if (existingUser != null)
                {
                    // Update existing user information
                    var updated = false;
                    
                    if (!string.IsNullOrEmpty(adResult.DisplayName) && existingUser.FullName != adResult.DisplayName)
                    {
                        existingUser.FullName = adResult.DisplayName;
                        updated = true;
                    }
                    
                    if (!string.IsNullOrEmpty(adResult.Email) && existingUser.Email != adResult.Email)
                    {
                        existingUser.Email = adResult.Email;
                        existingUser.NormalizedEmail = adResult.Email.ToUpperInvariant();
                        updated = true;
                    }

                    if (existingUser.ActiveDirectoryDn != adResult.DistinguishedName)
                    {
                        existingUser.ActiveDirectoryDn = adResult.DistinguishedName;
                        updated = true;
                    }

                    if (!existingUser.IsActiveDirectoryUser)
                    {
                        existingUser.IsActiveDirectoryUser = true;
                        updated = true;
                    }

                    if (updated)
                    {
                        await _userManager.UpdateAsync(existingUser);
                        _logger.LogInformation("Updated AD user information for: {Username}", username);
                        await _adLoggingService.LogOperationAsync("AD User Update", username, $"Updated existing user information from AD: {(string.IsNullOrEmpty(adResult.DisplayName) ? "No display name" : adResult.DisplayName)}");
                    }

                    // Update user roles based on AD group membership
                    await UpdateUserRolesAsync(existingUser, adResult);
                    
                    await _adLoggingService.LogOperationAsync("AD User Provision", username, $"Existing AD user provisioned successfully with roles: {string.Join(", ", await _userManager.GetRolesAsync(existingUser))}");
                    
                    return existingUser;
                }
                else
                {
                    // Create new user
                    var newUser = new ApplicationUser
                    {
                        UserName = username,
                        Email = adResult.Email ?? $"{username}@{_adSettings.Domain}",
                        EmailConfirmed = true, // AD users are considered confirmed
                        FullName = adResult.DisplayName ?? username,
                        IsActiveDirectoryUser = true,
                        ActiveDirectoryDn = adResult.DistinguishedName
                    };

                    // Set normalized email
                    newUser.NormalizedEmail = newUser.Email.ToUpperInvariant();
                    newUser.NormalizedUserName = username.ToUpperInvariant();

                    // Create user without password (AD users don't have local passwords)
                    var result = await _userManager.CreateAsync(newUser);
                    
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Created new AD user: {Username}", username);
                        
                        // Assign roles based on AD group membership
                        await UpdateUserRolesAsync(newUser, adResult);
                        
                        await _adLoggingService.LogOperationAsync("AD User Creation", username, $"New AD user created successfully with roles: {string.Join(", ", await _userManager.GetRolesAsync(newUser))}");
                        
                        return newUser;
                    }
                    else
                    {
                        var errorMsg = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to create AD user {Username}: {Errors}", username, errorMsg);
                        await _adLoggingService.LogOperationAsync("AD User Creation Failed", username, $"Failed to create new AD user: {errorMsg}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning AD user: {Username}", username);
                await _adLoggingService.LogOperationAsync("AD User Provision Error", username, $"Error provisioning AD user: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Determines user roles based on AD group membership
        /// </summary>
        public Task<string[]> DetermineUserRolesAsync(AdAuthenticationResult adResult)
        {
            var roles = new List<string>();

            if (!adResult.IsSuccessful || adResult.GroupMemberships == null)
            {
                // Default role if no group information available
                roles.Add("User");
                return Task.FromResult(roles.ToArray());
            }

            // Check for admin group membership
            if (!string.IsNullOrEmpty(_adSettings.AdminGroupDn) && 
                adResult.GroupMemberships.Any(group => group.Equals(_adSettings.AdminGroupDn, StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add("Admin");
            }

            // Check for user group membership
            if (!string.IsNullOrEmpty(_adSettings.UserGroupDn) && 
                adResult.GroupMemberships.Any(group => group.Equals(_adSettings.UserGroupDn, StringComparison.OrdinalIgnoreCase)))
            {
                roles.Add("User");
            }

            // If no specific groups matched, assign default User role
            if (roles.Count == 0)
            {
                roles.Add("User");
            }

            return Task.FromResult(roles.ToArray());
        }

        /// <summary>
        /// Updates user roles based on AD group membership
        /// </summary>
        private async Task UpdateUserRolesAsync(ApplicationUser user, AdAuthenticationResult adResult)
        {
            try
            {
                // Get current roles
                var currentRoles = await _userManager.GetRolesAsync(user);
                
                // Determine new roles based on AD groups
                var newRoles = await DetermineUserRolesAsync(adResult);
                
                // Remove roles that are no longer applicable
                var rolesToRemove = currentRoles.Except(newRoles).ToArray();
                if (rolesToRemove.Length > 0)
                {
                    await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    _logger.LogInformation("Removed roles {Roles} from user {Username}", 
                        string.Join(", ", rolesToRemove), user.UserName);
                }
                
                // Add new roles
                var rolesToAdd = newRoles.Except(currentRoles).ToArray();
                if (rolesToAdd.Length > 0)
                {
                    // Ensure roles exist
                    foreach (var roleName in rolesToAdd)
                    {
                        if (!await _roleManager.RoleExistsAsync(roleName))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(roleName));
                        }
                    }
                    
                    await _userManager.AddToRolesAsync(user, rolesToAdd);
                    _logger.LogInformation("Added roles {Roles} to user {Username}", 
                        string.Join(", ", rolesToAdd), user.UserName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating roles for user: {Username}", user.UserName);
            }
        }
    }
}