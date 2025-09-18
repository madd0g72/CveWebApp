using Microsoft.AspNetCore.Identity;

namespace CveWebApp.Models
{
    /// <summary>
    /// Application user model extending IdentityUser for role-based authentication
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        // Additional user properties can be added here if needed in the future
        public string? FullName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indicates whether this user authenticates via Active Directory
        /// </summary>
        public bool IsActiveDirectoryUser { get; set; } = false;

        /// <summary>
        /// Active Directory distinguished name for AD users
        /// </summary>
        public string? ActiveDirectoryDn { get; set; }
    }
}