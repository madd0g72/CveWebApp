using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// View model for user provisioning form
    /// </summary>
    public class UserProvisioningViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 4)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public string SelectedRole { get; set; } = string.Empty;

        /// <summary>
        /// Available roles for selection
        /// </summary>
        public List<string> AvailableRoles { get; set; } = new List<string> { "Admin", "User", "operator" };

        /// <summary>
        /// List of existing users for management
        /// </summary>
        public List<UserManagementItem> ExistingUsers { get; set; } = new List<UserManagementItem>();

        /// <summary>
        /// AD users in Admin group (if AD is configured)
        /// </summary>
        public List<AdUserDisplayInfo> AdAdminUsers { get; set; } = new List<AdUserDisplayInfo>();

        /// <summary>
        /// AD users in User group (if AD is configured)
        /// </summary>
        public List<AdUserDisplayInfo> AdUserGroupUsers { get; set; } = new List<AdUserDisplayInfo>();

        /// <summary>
        /// Whether AD integration is configured and enabled
        /// </summary>
        public bool IsAdConfigured { get; set; }

        /// <summary>
        /// AD Admin group name for display
        /// </summary>
        public string? AdAdminGroupName { get; set; }

        /// <summary>
        /// AD User group name for display
        /// </summary>
        public string? AdUserGroupName { get; set; }
    }

    /// <summary>
    /// Information about an AD user for display in the provisioning interface
    /// </summary>
    public class AdUserDisplayInfo
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsProvisioned { get; set; }
        public string LocalRoles { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a user in the management table
    /// </summary>
    public class UserManagementItem
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    /// <summary>
    /// View model for role management operations
    /// </summary>
    public class UserRoleManagementViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public List<string> CurrentRoles { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string> { "Admin", "User", "operator" };
        public List<string> SelectedRoles { get; set; } = new List<string>();
    }
}