using System.DirectoryServices.Protocols;
using System.Net;
using CveWebApp.Models;
using Microsoft.Extensions.Options;

namespace CveWebApp.Services
{
    /// <summary>
    /// Result of an Active Directory authentication attempt
    /// </summary>
    public class AdAuthenticationResult
    {
        public bool IsSuccessful { get; set; }
        public string? ErrorMessage { get; set; }
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
        public string? DistinguishedName { get; set; }
        public List<string> GroupMemberships { get; set; } = new List<string>();
    }

    /// <summary>
    /// Interface for Active Directory authentication service
    /// </summary>
    public interface IActiveDirectoryService
    {
        Task<AdAuthenticationResult> AuthenticateUserAsync(string username, string password);
        Task<AdAuthenticationResult> GetUserDetailsAsync(string username);
        Task<List<AdUserInfo>> GetUsersByGroupMembershipAsync(string groupDn);
        bool IsConfigured { get; }
    }

    /// <summary>
    /// Information about an AD user for display purposes
    /// </summary>
    public class AdUserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<string> GroupMemberships { get; set; } = new List<string>();
    }

    /// <summary>
    /// Service for Active Directory authentication using secure LDAP
    /// </summary>
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly ActiveDirectorySettings _adSettings;
        private readonly ILogger<ActiveDirectoryService> _logger;
        private readonly IActiveDirectoryLoggingService _adLoggingService;

        public ActiveDirectoryService(
            IOptions<ActiveDirectorySettings> adSettings, 
            ILogger<ActiveDirectoryService> logger,
            IActiveDirectoryLoggingService adLoggingService)
        {
            _adSettings = adSettings.Value;
            _logger = logger;
            _adLoggingService = adLoggingService;
        }

        public bool IsConfigured => _adSettings.Enabled && 
                                   !string.IsNullOrEmpty(_adSettings.Server) && 
                                   !string.IsNullOrEmpty(_adSettings.BaseDn) &&
                                   !string.IsNullOrEmpty(_adSettings.ServiceAccountUsername) &&
                                   !string.IsNullOrEmpty(_adSettings.ServiceAccountPassword);

        /// <summary>
        /// Authenticates a user against Active Directory using secure LDAP
        /// </summary>
        public async Task<AdAuthenticationResult> AuthenticateUserAsync(string username, string password)
        {
            if (!IsConfigured)
            {
                var errorMsg = "Active Directory is not configured";
                await _adLoggingService.LogAuthenticationAsync(username, false, errorMsg);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = errorMsg
                };
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                var errorMsg = "Username and password are required";
                await _adLoggingService.LogAuthenticationAsync(username ?? "NULL", false, errorMsg);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = errorMsg
                };
            }

            try
            {
                // First, get user details using service account
                var userDetails = await GetUserDetailsAsync(username);
                if (!userDetails.IsSuccessful || string.IsNullOrEmpty(userDetails.DistinguishedName))
                {
                    var errorMsg = "User not found in Active Directory";
                    await _adLoggingService.LogAuthenticationAsync(username, false, errorMsg);
                    return new AdAuthenticationResult 
                    { 
                        IsSuccessful = false, 
                        ErrorMessage = errorMsg
                    };
                }

                // Then authenticate the user with their credentials
                using (var connection = CreateLdapConnection())
                {
                    var userCredential = new NetworkCredential(userDetails.DistinguishedName, password);
                    connection.Credential = userCredential;

                    // Attempt to bind with user credentials
                    await Task.Run(() => connection.Bind());

                    _logger.LogInformation("AD authentication successful for user: {Username}", username);
                    
                    // Log successful authentication
                    await _adLoggingService.LogAuthenticationAsync(username, true);
                    
                    // Log group membership details
                    await _adLoggingService.LogGroupMembershipQueryAsync(username, userDetails.GroupMemberships);
                    
                    return new AdAuthenticationResult
                    {
                        IsSuccessful = true,
                        DisplayName = userDetails.DisplayName,
                        Email = userDetails.Email,
                        DistinguishedName = userDetails.DistinguishedName,
                        GroupMemberships = userDetails.GroupMemberships
                    };
                }
            }
            catch (LdapException ex)
            {
                var errorMsg = "Invalid username or password";
                _logger.LogWarning("AD authentication failed for user {Username}: {Error}", username, ex.Message);
                await _adLoggingService.LogAuthenticationAsync(username, false, errorMsg);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = errorMsg
                };
            }
            catch (Exception ex)
            {
                var errorMsg = "Authentication service unavailable";
                _logger.LogError(ex, "Unexpected error during AD authentication for user: {Username}", username);
                await _adLoggingService.LogAuthenticationAsync(username, false, $"{errorMsg}: {ex.Message}");
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = errorMsg
                };
            }
        }

        /// <summary>
        /// Gets user details from Active Directory using the service account
        /// </summary>
        public async Task<AdAuthenticationResult> GetUserDetailsAsync(string username)
        {
            if (!IsConfigured)
            {
                var errorMsg = "Active Directory is not configured";
                await _adLoggingService.LogUserLookupAsync(username, false, errorMsg);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = errorMsg
                };
            }

            try
            {
                using (var connection = CreateLdapConnection())
                {
                    // Bind with service account
                    var serviceCredential = new NetworkCredential(_adSettings.ServiceAccountUsername, _adSettings.ServiceAccountPassword);
                    connection.Credential = serviceCredential;
                    
                    await Task.Run(() => connection.Bind());

                    // Search for the user
                    var searchFilter = string.Format(_adSettings.UserSearchFilter, username);
                    var searchRequest = new SearchRequest(
                        _adSettings.BaseDn,
                        searchFilter,
                        SearchScope.Subtree,
                        new[] { 
                            _adSettings.DisplayNameAttribute, 
                            _adSettings.EmailAttribute, 
                            _adSettings.GroupMembershipAttribute,
                            "distinguishedName"
                        });

                    var response = (SearchResponse)await Task.Run(() => connection.SendRequest(searchRequest));

                    if (response.Entries.Count == 0)
                    {
                        var errorMsg = "User not found";
                        await _adLoggingService.LogUserLookupAsync(username, false, errorMsg);
                        return new AdAuthenticationResult 
                        { 
                            IsSuccessful = false, 
                            ErrorMessage = errorMsg
                        };
                    }

                    var entry = response.Entries[0];
                    var result = new AdAuthenticationResult
                    {
                        IsSuccessful = true,
                        DistinguishedName = GetAttributeValue(entry, "distinguishedName"),
                        DisplayName = GetAttributeValue(entry, _adSettings.DisplayNameAttribute),
                        Email = GetAttributeValue(entry, _adSettings.EmailAttribute)
                    };

                    // Get group memberships
                    var groupAttribute = entry.Attributes[_adSettings.GroupMembershipAttribute];
                    if (groupAttribute != null)
                    {
                        result.GroupMemberships = groupAttribute.GetValues(typeof(string))
                            .Cast<string>()
                            .ToList();
                    }

                    // Log successful lookup
                    await _adLoggingService.LogUserLookupAsync(username, true);

                    return result;
                }
            }
            catch (Exception ex)
            {
                var errorMsg = "Unable to retrieve user details";
                _logger.LogError(ex, "Error retrieving user details for: {Username}", username);
                await _adLoggingService.LogUserLookupAsync(username, false, $"{errorMsg}: {ex.Message}");
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = errorMsg
                };
            }
        }
        /// <summary>
        /// Gets users who are members of a specific AD group
        /// </summary>
        public async Task<List<AdUserInfo>> GetUsersByGroupMembershipAsync(string groupDn)
        {
            var users = new List<AdUserInfo>();
            
            if (!IsConfigured || string.IsNullOrEmpty(groupDn))
            {
                await _adLoggingService.LogOperationAsync("AD Group Query Failed", "System", $"Cannot query group membership - AD not configured or group DN empty: {groupDn}");
                return users;
            }

            try
            {
                using (var connection = CreateLdapConnection())
                {
                    // Bind with service account
                    var serviceCredential = new NetworkCredential(_adSettings.ServiceAccountUsername, _adSettings.ServiceAccountPassword);
                    connection.Credential = serviceCredential;
                    
                    await Task.Run(() => connection.Bind());

                    // Search for users who are members of the specified group
                    var searchFilter = $"(&(objectClass=user)(memberOf={groupDn}))";
                    var searchRequest = new SearchRequest(
                        _adSettings.BaseDn,
                        searchFilter,
                        SearchScope.Subtree,
                        new[] { 
                            "sAMAccountName",
                            _adSettings.DisplayNameAttribute, 
                            _adSettings.EmailAttribute, 
                            _adSettings.GroupMembershipAttribute
                        });

                    var response = (SearchResponse)await Task.Run(() => connection.SendRequest(searchRequest));

                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        var userInfo = new AdUserInfo
                        {
                            Username = GetAttributeValue(entry, "sAMAccountName") ?? "",
                            DisplayName = GetAttributeValue(entry, _adSettings.DisplayNameAttribute) ?? "",
                            Email = GetAttributeValue(entry, _adSettings.EmailAttribute) ?? ""
                        };

                        // Get group memberships
                        var groupAttribute = entry.Attributes[_adSettings.GroupMembershipAttribute];
                        if (groupAttribute != null)
                        {
                            userInfo.GroupMemberships = groupAttribute.GetValues(typeof(string))
                                .Cast<string>()
                                .ToList();
                        }

                        if (!string.IsNullOrEmpty(userInfo.Username))
                        {
                            users.Add(userInfo);
                        }
                    }

                    await _adLoggingService.LogOperationAsync("AD Group Query Success", "System", $"Retrieved {users.Count} users from group: {groupDn}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users for group: {GroupDn}", groupDn);
                await _adLoggingService.LogOperationAsync("AD Group Query Failed", "System", $"Error retrieving users for group {groupDn}: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// Creates a configured LDAP connection
        /// </summary>
        private LdapConnection CreateLdapConnection()
        {
            var identifier = new LdapDirectoryIdentifier(_adSettings.Server, _adSettings.Port);
            var connection = new LdapConnection(identifier);
            
            // Force secure connection
            if (_adSettings.UseSsl)
            {
                connection.SessionOptions.SecureSocketLayer = true;
            }
            
            // Set timeout
            connection.Timeout = TimeSpan.FromSeconds(_adSettings.TimeoutSeconds);
            
            // Set authentication type
            connection.AuthType = AuthType.Basic;

            return connection;
        }

        /// <summary>
        /// Safely gets an attribute value from a directory entry
        /// </summary>
        private static string? GetAttributeValue(SearchResultEntry entry, string attributeName)
        {
            var attribute = entry.Attributes[attributeName];
            if (attribute == null || attribute.Count == 0)
                return null;

            return attribute[0]?.ToString();
        }
    }
}