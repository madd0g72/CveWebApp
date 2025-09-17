using System.Collections.Concurrent;
using System.Text.Json;

namespace CveWebApp.Services
{
    /// <summary>
    /// Custom file logger provider for capturing framework and application logs
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly object _lockObject = new object();

        public FileLoggerProvider(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _configuration, _environment, _lockObject));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    /// <summary>
    /// Custom file logger for framework and application logs
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly object _lockObject;

        public FileLogger(string categoryName, IConfiguration configuration, IWebHostEnvironment environment, object lockObject)
        {
            _categoryName = categoryName;
            _configuration = configuration;
            _environment = environment;
            _lockObject = lockObject;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Check if file logging is enabled
            if (!_configuration.GetValue<bool>("FileLogging:Enabled", false))
                return false;

            // Check if application logging is enabled
            if (!_configuration.GetValue<bool>("FileLogging:ApplicationLoggingEnabled", false))
                return false;

            // Get minimum log level from configuration
            var minLogLevel = _configuration.GetValue<LogLevel>("FileLogging:MinimumLogLevel", LogLevel.Information);
            return logLevel >= minLogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            try
            {
                var message = formatter(state, exception);
                
                // Skip empty messages
                if (string.IsNullOrWhiteSpace(message))
                    return;

                var logEntry = new
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "Application",
                    LogLevel = logLevel.ToString(),
                    Category = _categoryName,
                    EventId = eventId.Id,
                    EventName = eventId.Name,
                    Message = message,
                    Exception = exception?.ToString(),
                    Environment = _environment.EnvironmentName
                };

                var logPath = GetApplicationLogPath();
                WriteLogEntryAsync(logPath, logEntry).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Failed to write application log: {ex.Message}");
            }
        }

        private string GetApplicationLogPath()
        {
            var basePath = _configuration.GetValue<string>("FileLogging:ApplicationLogPath");
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.Combine(_environment.ContentRootPath, "Logs", "application.log");
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
                WriteIndented = false
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