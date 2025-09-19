using System.Text.Json;
using System.Text.Encodings.Web;

namespace CveWebApp.Services
{
    /// <summary>
    /// Service for logging application actions and errors to file
    /// </summary>
    public interface IFileLoggingService
    {
        Task LogActionAsync(string action, string username, string details, string? sourceIP = null);
        Task LogErrorAsync(string error, string? username = null, string? sourceIP = null, Exception? exception = null);
        bool IsLoggingEnabled { get; }
    }

    public class FileLoggingService : IFileLoggingService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly object _lockObject = new object();

        public FileLoggingService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public bool IsLoggingEnabled => _configuration.GetValue<bool>("FileLogging:Enabled", false);

        public async Task LogActionAsync(string action, string username, string details, string? sourceIP = null)
        {
            if (!IsLoggingEnabled) return;

            try
            {
                var logEntry = new
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "Action",
                    Action = action,
                    Username = username,
                    Details = details,
                    SourceIP = sourceIP ?? "Unknown",
                    Environment = _environment.EnvironmentName
                };

                var logPath = GetActionLogPath();
                await WriteLogEntryAsync(logPath, logEntry);
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Failed to write action log: {ex.Message}");
            }
        }

        public async Task LogErrorAsync(string error, string? username = null, string? sourceIP = null, Exception? exception = null)
        {
            if (!IsLoggingEnabled) return;

            try
            {
                var logEntry = new
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "Error",
                    Error = error,
                    Username = username ?? "Anonymous",
                    SourceIP = sourceIP ?? "Unknown",
                    ExceptionType = exception?.GetType().Name,
                    ExceptionMessage = exception?.Message,
                    StackTrace = exception?.StackTrace,
                    Environment = _environment.EnvironmentName
                };

                var logPath = GetErrorLogPath();
                await WriteLogEntryAsync(logPath, logEntry);
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Failed to write error log: {ex.Message}");
            }
        }

        private string GetActionLogPath()
        {
            var basePath = _configuration.GetValue<string>("FileLogging:ActionLogPath");
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.Combine(_environment.ContentRootPath, "Logs", "actions.log");
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(basePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return basePath;
        }

        private string GetErrorLogPath()
        {
            var basePath = _configuration.GetValue<string>("FileLogging:ErrorLogPath");
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.Combine(_environment.ContentRootPath, "Logs", "errors.log");
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