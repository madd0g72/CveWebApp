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
        bool IsConfigured { get; }
    }

    /// <summary>
    /// Service for Active Directory authentication using secure LDAP
    /// </summary>
    public class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly ActiveDirectorySettings _adSettings;
        private readonly ILogger<ActiveDirectoryService> _logger;

        public ActiveDirectoryService(IOptions<ActiveDirectorySettings> adSettings, ILogger<ActiveDirectoryService> logger)
        {
            _adSettings = adSettings.Value;
            _logger = logger;
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
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = "Active Directory is not configured" 
                };
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = "Username and password are required" 
                };
            }

            try
            {
                // First, get user details using service account
                var userDetails = await GetUserDetailsAsync(username);
                if (!userDetails.IsSuccessful || string.IsNullOrEmpty(userDetails.DistinguishedName))
                {
                    return new AdAuthenticationResult 
                    { 
                        IsSuccessful = false, 
                        ErrorMessage = "User not found in Active Directory" 
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
                _logger.LogWarning("AD authentication failed for user {Username}: {Error}", username, ex.Message);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = "Invalid username or password" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during AD authentication for user: {Username}", username);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = "Authentication service unavailable" 
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
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = "Active Directory is not configured" 
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
                        return new AdAuthenticationResult 
                        { 
                            IsSuccessful = false, 
                            ErrorMessage = "User not found" 
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

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user details for: {Username}", username);
                return new AdAuthenticationResult 
                { 
                    IsSuccessful = false, 
                    ErrorMessage = "Unable to retrieve user details" 
                };
            }
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