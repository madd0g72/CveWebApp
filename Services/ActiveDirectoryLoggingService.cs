using System.Text.Json;
using System.Text.Encodings.Web;

namespace CveWebApp.Services
{
    /// <summary>
    /// Service for logging Active Directory operations, lookups, and authentications
    /// </summary>
    public interface IActiveDirectoryLoggingService
    {
        Task LogAuthenticationAsync(string username, bool isSuccessful, string? errorMessage = null, string? sourceIP = null);
        Task LogUserLookupAsync(string username, bool isSuccessful, string? errorMessage = null);
        Task LogGroupMembershipQueryAsync(string username, List<string> groups, string? errorMessage = null);
        Task LogOperationAsync(string operation, string username, string details, string? sourceIP = null);
        bool IsLoggingEnabled { get; }
    }

    public class ActiveDirectoryLoggingService : IActiveDirectoryLoggingService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ActiveDirectoryLoggingService> _logger;
        private readonly object _lockObject = new object();

        public ActiveDirectoryLoggingService(
            IConfiguration configuration, 
            IWebHostEnvironment environment,
            ILogger<ActiveDirectoryLoggingService> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public bool IsLoggingEnabled => _configuration.GetValue<bool>("FileLogging:Enabled", false);

        public async Task LogAuthenticationAsync(string username, bool isSuccessful, string? errorMessage = null, string? sourceIP = null)
        {
            var operation = isSuccessful ? "AD Authentication Success" : "AD Authentication Failed";
            var details = isSuccessful 
                ? $"User '{username}' successfully authenticated via Active Directory"
                : $"Authentication failed for user '{username}': {errorMessage}";

            await LogOperationAsync(operation, username, details, sourceIP);

            // Also log to standard application logger
            if (isSuccessful)
            {
                _logger.LogInformation("AD Authentication successful for user: {Username} from {SourceIP}", username, sourceIP ?? "Unknown");
            }
            else
            {
                _logger.LogWarning("AD Authentication failed for user: {Username} from {SourceIP}. Error: {Error}", username, sourceIP ?? "Unknown", errorMessage);
            }
        }

        public async Task LogUserLookupAsync(string username, bool isSuccessful, string? errorMessage = null)
        {
            var operation = isSuccessful ? "AD User Lookup Success" : "AD User Lookup Failed";
            var details = isSuccessful 
                ? $"User '{username}' found in Active Directory"
                : $"User lookup failed for '{username}': {errorMessage}";

            await LogOperationAsync(operation, username, details);

            // Also log to standard application logger
            if (isSuccessful)
            {
                _logger.LogDebug("AD User lookup successful for: {Username}", username);
            }
            else
            {
                _logger.LogWarning("AD User lookup failed for: {Username}. Error: {Error}", username, errorMessage);
            }
        }

        public async Task LogGroupMembershipQueryAsync(string username, List<string> groups, string? errorMessage = null)
        {
            var operation = string.IsNullOrEmpty(errorMessage) ? "AD Group Membership Query" : "AD Group Membership Query Failed";
            var details = string.IsNullOrEmpty(errorMessage)
                ? $"Retrieved group memberships for '{username}': {string.Join(", ", groups.Take(5))}{(groups.Count > 5 ? $" (+{groups.Count - 5} more)" : "")}"
                : $"Failed to retrieve group memberships for '{username}': {errorMessage}";

            await LogOperationAsync(operation, username, details);

            // Also log to standard application logger
            if (string.IsNullOrEmpty(errorMessage))
            {
                _logger.LogDebug("AD Group membership retrieved for user: {Username}. Groups: {GroupCount}", username, groups.Count);
            }
            else
            {
                _logger.LogWarning("AD Group membership query failed for: {Username}. Error: {Error}", username, errorMessage);
            }
        }

        public async Task LogOperationAsync(string operation, string username, string details, string? sourceIP = null)
        {
            if (!IsLoggingEnabled) return;

            try
            {
                var logEntry = new
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "ActiveDirectory",
                    Operation = operation,
                    Username = username,
                    Details = details,
                    SourceIP = sourceIP ?? "N/A",
                    Environment = _environment.EnvironmentName
                };

                var logPath = GetAdLogPath();
                await WriteLogEntryAsync(logPath, logEntry);
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                _logger.LogError(ex, "Failed to write AD operation log for user: {Username}", username);
                Console.WriteLine($"Failed to write AD operation log: {ex.Message}");
            }
        }

        private string GetAdLogPath()
        {
            // Get custom AD log path or create default
            var basePath = _configuration.GetValue<string>("FileLogging:ActiveDirectoryLogPath");
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.Combine(_environment.ContentRootPath, "Logs", "activedirectory.log");
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(basePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return basePath;
        }

        private async Task WriteLogEntryAsync(string logPath, object logEntry)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var jsonLine = JsonSerializer.Serialize(logEntry, jsonOptions);

            // Use lock for thread safety when writing to file
            await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    File.AppendAllText(logPath, jsonLine + Environment.NewLine);
                }
            });
        }
    }
}