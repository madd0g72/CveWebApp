# Environment Setup Guide

This document provides clear instructions for setting up the CveWebApp in both development and production environments with proper database separation.

## Environment Overview

### Development Environment
- **Database Provider**: In-memory database (for testing and development)
- **Configuration File**: `appsettings.Development.json`
- **Features**: Test data seeding, debug logging, development user accounts

### Production Environment
- **Database Provider**: SQL Server Express
- **Configuration File**: `appsettings.Production.json`
- **Default Database**: `cvewebapp_prod`
- **Features**: Production logging, no test data seeding

## Quick Setup

### Development Setup

**Option: Using In-Memory Database (Default)**
```bash
# No database setup required - uses in-memory database
# Run the application
dotnet run --environment Development
```

### Production Setup

1. **Install SQL Server Express**
   - Download and install SQL Server Express
   - Ensure the instance is running (typically `localhost\SQLEXPRESS`)

2. **Configure Connection String**
   ```json
   // appsettings.Production.json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=cvewebapp_prod;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. **Deploy and Run**
   ```bash
   dotnet publish -c Release
   dotnet run --environment Production
   ```

## Configuration Files

### appsettings.json (Base Configuration)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DatabaseProvider": "InMemory",
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "DatabaseProvider": "InMemory",
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=cvewebapp_prod;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## Database Migration Strategy

The application uses an intelligent database strategy:

### For Development
- Uses in-memory database for fast testing and development
- Automatically creates database schema on first run using EnsureCreated()
- No external database setup required

### For Production (SQL Server)
- Uses `EnsureCreated()` to create database schema
- Creates database schema compatible with SQL Server
- Idempotent - safe to run multiple times

### First Startup Behavior
1. **Development**: Creates in-memory database with schema and test data
2. **Production**: Creates SQL Server database with schema (no test data)

## Environment Validation

The application includes built-in validation to prevent environment mismatches:

- **Development**: Always uses in-memory database
- **Production**: Expects SQL Server provider
- **Mismatch Error**: Throws clear error message if wrong provider is detected

## Development Features

### Test Data Seeding (Development Only)
- **Default Admin**: `admin@cveapp.local` / `admin123`
- **Default User**: `user@cveapp.local` / `user123`
- **Sample CVE Data**: Pre-populated test records
- **Sample Server Data**: Test compliance scenarios

### Login Credentials (Development)
```
Admin User:
- Email: admin@cveapp.local
- Password: admin123
- Role: Admin

Regular User:
- Email: user@cveapp.local  
- Password: user123
- Role: User
```

## Security Considerations

### Development Environment
- ⚠️ Uses simplified password policies for testing
- ⚠️ Contains test accounts with known passwords
- ⚠️ Debug logging may expose sensitive information

### Production Environment
- ✅ No test data or accounts created
- ✅ Production-level logging
- ✅ Secure connection strings required
- ✅ Should use HTTPS in production deployment

## Troubleshooting

### Common Issues

1. **Database Connection Failed**
   - Verify database server is running
   - Check connection string format
   - Ensure database user has proper permissions

2. **Environment Mismatch Error**
   - Check `ASPNETCORE_ENVIRONMENT` variable
   - Verify correct `appsettings.{Environment}.json` exists
   - Ensure `DatabaseProvider` matches expected provider

3. **Schema Issues**
   - For SQL Server: Application uses EnsureCreated, not migrations
   - For development: Uses in-memory database (no external dependencies)
   - Clear database and restart for clean schema

### Environment Variable Options
```bash
# Set environment explicitly
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Production

# Run with specific environment
dotnet run --environment Development
dotnet run --environment Production
```

## Best Practices

1. **Never use development settings in production**
2. **Always use secure connection strings in production**
3. **Regularly backup production databases**
4. **Use proper SSL/TLS certificates in production**
5. **Review and harden password policies for production**
6. **Monitor application logs for security issues**

## Support

For issues or questions:
1. Check this documentation first
2. Verify environment configuration
3. Check application logs for specific error messages
4. Ensure database connectivity and permissions