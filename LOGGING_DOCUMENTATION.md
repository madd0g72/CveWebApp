# File Logging Documentation

## Overview

The CVE Web Application now includes comprehensive file-based logging functionality that tracks all application actions, errors, and system-level events. This feature is configurable and can be enabled or disabled by administrators. The system captures both custom application actions and framework-level operations including database activities, ASP.NET Core pipeline events, and authentication operations.

## Configuration

### Settings Location
The logging configuration is located in `appsettings.json`, `appsettings.Development.json`, and `appsettings.Production.json` files.

### Configuration Structure
```json
{
  "FileLogging": {
    "Enabled": true,
    "ActionLogPath": "Logs/actions.log",
    "ErrorLogPath": "Logs/errors.log",
    "ApplicationLoggingEnabled": true,
    "ApplicationLogPath": "Logs/application.log",
    "MinimumLogLevel": "Information"
  }
}
```

### Configuration Options

| Setting | Description | Default Value |
|---------|-------------|---------------|
| `Enabled` | Enable/disable file logging | `false` (base), `true` (dev), `true` (prod) |
| `ActionLogPath` | Path to action log file | `"Logs/actions.log"` |
| `ErrorLogPath` | Path to error log file | `"Logs/errors.log"` |
| `ApplicationLoggingEnabled` | Enable/disable framework logging | `false` (base), `true` (dev), `true` (prod) |
| `ApplicationLogPath` | Path to application/framework log file | `"Logs/application.log"` |
| `MinimumLogLevel` | Minimum log level for application logs | `"Information"` (dev), `"Warning"` (prod) |

### Environment-Specific Settings

- **Development**: All logging enabled, `Information` level, logs to `Logs/` directory
- **Production**: All logging enabled, `Warning` level, logs to `/var/log/cvewebapp/` directory
- **Base**: Disabled by default for backwards compatibility

## Log File Types

### 1. Action Log (`actions.log`)
Custom application actions and user interactions in JSON format.

### 2. Error Log (`errors.log`) 
Application errors and exceptions in JSON format.

### 3. Application Log (`application.log`) **NEW**
Framework-level events, database operations, and system logs in JSON format.

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

### Application Log Format **NEW**
Framework and system events are logged in JSON format:
```json
{
  "Timestamp": "2025-09-17T12:45:24.9577412Z",
  "Type": "Application",
  "LogLevel": "Information",
  "Category": "Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker",
  "EventId": 102,
  "EventName": "ControllerActionExecuting",
  "Message": "Route matched with {action = \"Index\", controller = \"Cve\"}. Executing controller action...",
  "Exception": null,
  "Environment": "Development"
}
```

## Logged Events

### Custom Application Actions
- User authentication events (login/logout/failures)
- CVE dashboard access and search operations
- CSV import/export activities (admin functions)
- PDF compliance report generation
- All actions include IP tracking and user identification

### Framework and System Events **NEW**
- **ASP.NET Core Request Pipeline**: Request start/finish, routing, middleware execution
- **Controller Actions**: Action execution, parameter binding, result execution
- **View Rendering**: View execution timing and performance metrics
- **Entity Framework**: Database queries, change tracking, save operations
- **Authentication**: Identity operations, role assignments, token validation
- **Data Protection**: Key generation, encryption operations
- **Hosting Events**: Application startup, shutdown, configuration loading

### Error Events
- Database errors
- File processing errors
- Authentication errors
- Application exceptions
- Framework-level errors and warnings

## Implementation Details

### Service Architecture
The logging functionality is implemented through multiple components:

1. **IFileLoggingService**: Custom application action and error logging
2. **FileLoggerProvider**: ASP.NET Core logging provider for framework events
3. **FileLogger**: Custom logger implementation for structured logging

### Framework Integration
The system integrates with the .NET logging infrastructure to capture:
- Entity Framework database operations with sensitive data logging (development only)
- ASP.NET Core pipeline events at Information level or higher
- Authentication and authorization events
- Performance metrics and timing data

### Thread Safety
All logging operations use file locking mechanisms to ensure thread-safe writing to log files.

### Performance Considerations
- Logging operations are asynchronous to minimize impact on application performance
- Failed logging operations fall back to console output and do not interrupt normal application flow
- Configurable log levels allow filtering to reduce log volume in production

### Security Features
- IP address tracking for all actions
- User identification for authenticated actions
- Sensitive data logging only enabled in development environment
- No passwords, tokens, or other secrets logged

## Administration

### Enabling/Disabling Action and Error Logging
To enable or disable custom action and error logging:

```json
{
  "FileLogging": {
    "Enabled": false  // Set to true to enable, false to disable
  }
}
```

### Enabling/Disabling Framework Logging **NEW**
To enable or disable comprehensive framework and database logging:

```json
{
  "FileLogging": {
    "ApplicationLoggingEnabled": false  // Set to true to enable, false to disable
  }
}
```

### Configuring Log Levels **NEW**
To control the verbosity of framework logging:

```json
{
  "FileLogging": {
    "MinimumLogLevel": "Warning"  // Options: Trace, Debug, Information, Warning, Error, Critical
  }
}
```

### Changing Log File Locations
Administrators can configure custom log file paths:

```json
{
  "FileLogging": {
    "ActionLogPath": "/custom/path/actions.log",
    "ErrorLogPath": "/custom/path/errors.log",
    "ApplicationLogPath": "/custom/path/application.log"
  }
}
```

### Log File Management
- Log files are automatically created when needed
- Parent directories are created automatically if they don't exist
- Log files use append mode to preserve historical data
- Administrators should implement log rotation policies as needed
- Application logs can generate significant volume - monitor disk space

## Troubleshooting

### Common Issues

1. **Log files not created**: 
   - Check that `FileLogging.Enabled` is set to `true`
   - For framework logs, ensure `ApplicationLoggingEnabled` is `true`
2. **Permission denied**: Ensure the application has write permissions to the log directory
3. **Directory not found**: The application will create directories automatically, but parent paths must exist
4. **Large log files**: Application logs can grow quickly - implement log rotation as needed

### Log Volume Management **NEW**
Framework logging can generate significant volume. To manage this:
- Use higher log levels (`Warning` or `Error`) in production
- Implement log rotation policies
- Monitor disk space usage
- Consider filtering specific categories if needed

### Fallback Behavior
If file logging fails, the application will:
- Continue normal operation without interruption
- Output error messages to the console
- Not retry failed logging operations to prevent performance impact

## Security Considerations

- Log files may contain sensitive information such as IP addresses and usernames
- Framework logs may include request/response data (not sensitive in current implementation)
- Ensure appropriate file permissions are set on log directories
- Consider log retention policies for compliance requirements
- Monitor log file sizes to prevent disk space issues
- Sensitive data logging is only enabled in development environment

## Integration Points

The logging system is integrated into the following components:

### Custom Application Logging
- `AccountController`: Login/logout actions and authentication errors
- `CveController`: CVE operations and import/export activities
- `HomeController`: Search operations and general application errors

### Framework Logging **NEW**
- **ASP.NET Core Pipeline**: Automatic capture of all request/response cycles
- **Entity Framework**: Database operations, queries, and change tracking
- **Authentication System**: Identity operations and security events
- **Data Protection**: Key management and encryption operations
- **Hosting Infrastructure**: Application lifecycle events

Additional controllers can be easily integrated by injecting the `IFileLoggingService` and calling the appropriate logging methods.