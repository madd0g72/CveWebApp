# File Logging Documentation

## Overview

The CVE Web Application now includes comprehensive file-based logging functionality that tracks all application actions and errors. This feature is configurable and can be enabled or disabled by administrators.

## Configuration

### Settings Location
The logging configuration is located in `appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json` files.

### Configuration Structure
```json
{
  "FileLogging": {
    "Enabled": true,
    "ActionLogPath": "Logs/actions.log",
    "ErrorLogPath": "Logs/errors.log"
  }
}
```

### Configuration Options

| Setting | Description | Default Value |
|---------|-------------|---------------|
| `Enabled` | Enable/disable file logging | `false` (base), `true` (dev), `true` (prod) |
| `ActionLogPath` | Path to action log file | `"Logs/actions.log"` |
| `ErrorLogPath` | Path to error log file | `"Logs/errors.log"` |

### Environment-Specific Settings

- **Development**: Enabled by default, logs to `Logs/` directory
- **Production**: Enabled by default, logs to `/var/log/cvewebapp/` directory
- **Base**: Disabled by default for backwards compatibility

## Log File Formats

### Action Log Format
Actions are logged in JSON format with the following structure:
```json
{
  "Timestamp": "2025-09-17T12:33:04.68219Z",
  "Type": "Action",
  "Action": "User Login",
  "Username": "admin@cveapp.local",
  "Details": "Successful login from ::1",
  "SourceIP": "::1",
  "Environment": "Development"
}
```

### Error Log Format
Errors are logged in JSON format with the following structure:
```json
{
  "Timestamp": "2025-09-17T12:35:04.123456Z",
  "Type": "Error",
  "Error": "Database connection failed",
  "Username": "admin@cveapp.local",
  "SourceIP": "192.168.1.100",
  "ExceptionType": "SqlException",
  "ExceptionMessage": "Cannot open database",
  "StackTrace": "...",
  "Environment": "Production"
}
```

## Logged Actions

### Authentication Actions
- User login (successful and failed attempts)
- User logout
- Access denied events

### CVE Management Actions
- CVE Dashboard access
- CVE search operations
- CVE import operations (admin only)
- CVE export operations (PDF/CSV downloads)
- Compliance report generation

### Error Events
- Database errors
- File processing errors
- Authentication errors
- Application exceptions

## Implementation Details

### Service Architecture
The logging functionality is implemented through the `IFileLoggingService` interface with the following methods:

- `LogActionAsync(string action, string username, string details, string? sourceIP = null)`
- `LogErrorAsync(string error, string? username = null, string? sourceIP = null, Exception? exception = null)`
- `IsLoggingEnabled` property

### Thread Safety
The logging service uses file locking mechanisms to ensure thread-safe writing to log files.

### Performance Considerations
- Logging operations are asynchronous to minimize impact on application performance
- Failed logging operations fall back to console output and do not interrupt normal application flow

### Security Features
- IP address tracking for all actions
- User identification for authenticated actions
- Sensitive data is not logged (passwords, tokens, etc.)

## Administration

### Enabling/Disabling Logging
To enable or disable logging, modify the `FileLogging.Enabled` setting in the appropriate `appsettings.json` file:

```json
{
  "FileLogging": {
    "Enabled": false  // Set to true to enable, false to disable
  }
}
```

### Changing Log File Locations
Administrators can configure custom log file paths:

```json
{
  "FileLogging": {
    "ActionLogPath": "/custom/path/actions.log",
    "ErrorLogPath": "/custom/path/errors.log"
  }
}
```

### Log File Management
- Log files are automatically created when needed
- Parent directories are created automatically if they don't exist
- Log files use append mode to preserve historical data
- Administrators should implement log rotation policies as needed

## Troubleshooting

### Common Issues

1. **Log files not created**: Check that `FileLogging.Enabled` is set to `true`
2. **Permission denied**: Ensure the application has write permissions to the log directory
3. **Directory not found**: The application will create directories automatically, but parent paths must exist

### Fallback Behavior
If file logging fails, the application will:
- Continue normal operation without interruption
- Output error messages to the console
- Not retry failed logging operations to prevent performance impact

## Security Considerations

- Log files may contain sensitive information such as IP addresses and usernames
- Ensure appropriate file permissions are set on log directories
- Consider log retention policies for compliance requirements
- Monitor log file sizes to prevent disk space issues

## Integration Points

The logging service is integrated into the following controllers:
- `AccountController`: Login/logout actions and authentication errors
- `CveController`: CVE operations and import/export activities
- `HomeController`: Search operations and general application errors

Additional controllers can be easily integrated by injecting the `IFileLoggingService` and calling the appropriate logging methods.