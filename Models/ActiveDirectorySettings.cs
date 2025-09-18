namespace CveWebApp.Models
{
    /// <summary>
    /// Configuration settings for Active Directory authentication
    /// </summary>
    public class ActiveDirectorySettings
    {
        /// <summary>
        /// Whether Active Directory authentication is enabled
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// LDAP server hostname or IP address
        /// </summary>
        public string? Server { get; set; }

        /// <summary>
        /// LDAP port (typically 636 for LDAPS)
        /// </summary>
        public int Port { get; set; } = 636;

        /// <summary>
        /// Whether to use SSL/TLS for LDAP connections (should be true for production)
        /// </summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// Base DN for user searches (e.g., "CN=Users,DC=company,DC=local")
        /// </summary>
        public string? BaseDn { get; set; }

        /// <summary>
        /// Domain name (e.g., "company.local")
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Service account username for LDAP binds
        /// </summary>
        public string? ServiceAccountUsername { get; set; }

        /// <summary>
        /// Service account password for LDAP binds
        /// </summary>
        public string? ServiceAccountPassword { get; set; }

        /// <summary>
        /// LDAP filter for user searches (e.g., "(&(objectClass=user)(sAMAccountName={0}))")
        /// </summary>
        public string UserSearchFilter { get; set; } = "(&(objectClass=user)(sAMAccountName={0}))";

        /// <summary>
        /// Attribute name for user's display name (e.g., "displayName")
        /// </summary>
        public string DisplayNameAttribute { get; set; } = "displayName";

        /// <summary>
        /// Attribute name for user's email address (e.g., "mail")
        /// </summary>
        public string EmailAttribute { get; set; } = "mail";

        /// <summary>
        /// Attribute name for group membership (e.g., "memberOf")
        /// </summary>
        public string GroupMembershipAttribute { get; set; } = "memberOf";

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// AD group DN that should be mapped to Admin role
        /// </summary>
        public string? AdminGroupDn { get; set; }

        /// <summary>
        /// AD group DN that should be mapped to User role
        /// </summary>
        public string? UserGroupDn { get; set; }

        /// <summary>
        /// Whether to allow local user authentication as fallback
        /// </summary>
        public bool AllowLocalUserFallback { get; set; } = true;
    }
}