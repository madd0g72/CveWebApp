# CVE Web Application

**ALWAYS** follow these instructions first and only search for additional context if the information here is incomplete or incorrect.

## Overview

CVE Web Application is an ASP.NET Core MVC application for managing and importing CVE (Common Vulnerabilities and Exposures) data with role-based authentication, CSV import functionality, and database integration. Built with .NET 8, Entity Framework Core, and Bootstrap for responsive UI.

## Prerequisites & Setup

### Required Software
- .NET 8 SDK (verified version: 8.0.119)

### Initial Setup Commands
```bash
dotnet restore    # Downloads dependencies (~14 seconds)
dotnet build      # Debug build (~12 seconds, Release build ~5 seconds)
```

## Build & Run Instructions

### Build Commands
- **Development build**: `dotnet build` - Takes ~12 seconds. NEVER CANCEL - always wait for completion.
- **Release build**: `dotnet build --configuration Release` - Takes ~5 seconds. NEVER CANCEL.
- **Publish**: `dotnet publish --configuration Release` - Takes ~2 seconds. NEVER CANCEL.

**CRITICAL**: All builds complete within 15 seconds. Set timeouts to 60+ seconds to prevent premature cancellation.

### Running the Application
```bash
dotnet run --no-https    # Starts on http://localhost:5255 (~10 seconds startup)
```

**IMPORTANT**: 
- Application uses in-memory database by default when no connection string is configured
- For testing, remove ConnectionStrings section from `appsettings.Development.json`
- Startup takes ~10 seconds with database seeding
- Always use `--no-https` flag to avoid certificate issues in development

### Database Configuration
- **In-memory (default)**: Always used for development environment
- **SQL Server**: Set `DatabaseProvider` to "SqlServer" in configuration for production

## Testing & Validation

### Manual Validation Scenarios
After making changes, ALWAYS test these complete user workflows:

1. **Login Flow**:
   - Navigate to http://localhost:5255
   - Click "Login"
   - Use admin credentials: `admin@cveapp.local` / `admin123`
   - Verify "Admin Tools" dropdown appears
   - Use user credentials: `user@cveapp.local` / `user123`
   - Verify no admin tools visible

2. **CVE Dashboard**:
   - Click "CVE Dashboard"
   - Verify data table displays with test CVE records
   - Test filtering by Product Family, Product, Severity
   - Click "Details" link on any CVE record

3. **Admin Import (admin only)**:
   - Login as admin
   - Click "Admin Tools" → "Import CVE Data"
   - Verify CSV upload interface displays
   - Test file upload functionality

### Build Validation
- **No tests exist** - application has no test suite
- Build warnings are expected (9 warnings related to nullable reference types)
- Always run `dotnet build` after code changes
- Release builds are preferred for performance testing

## Project Structure

### Key Directories
- `/Controllers/` - MVC controllers (CveController, AccountController, etc.)
- `/Views/` - Razor views organized by controller
- `/Models/` - Entity models and view models
- `/Data/` - Entity Framework DbContext and migrations
- `/wwwroot/` - Static web assets (CSS, JS, images)
- `/Migrations/` - Entity Framework migrations

### Important Files
- `Program.cs` - Application configuration and startup
- `CveWebApp.csproj` - Project file with package references
- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `launchSettings.json` - Development server settings

### Key Models
- `CveUpdateStaging` - Main CVE data entity
- `ApplicationUser` - Extended Identity user
- `ServerInstalledKb` - Server KB installation tracking
- `KbSupersedence` - KB supersedence relationships

## Authentication & Authorization

### User Roles
- **Admin**: Full access to all features including import functionality
- **User**: Read-only access to CVE dashboard and details

### Demo Credentials
- **Admin**: `admin@cveapp.local` / `admin123`
- **User**: `user@cveapp.local` / `user123`

### Security Features
- ASP.NET Core Identity integration
- Role-based access control for admin features
- Simplified password policies for development (4 char minimum)

## Common Development Tasks

### Adding New Features
1. Create controller in `/Controllers/`
2. Add corresponding views in `/Views/[ControllerName]/`
3. Update navigation in `/Views/Shared/_Layout.cshtml`
4. Test authentication/authorization if required

### Database Changes
1. Modify models in `/Models/`
2. Update `ApplicationDbContext.cs`
3. Create migration: `dotnet ef migrations add [MigrationName]` (requires EF tools)
4. Apply migration: `dotnet ef database update` (requires EF tools)

### CSV Import Features
- Import functionality in `CveController.cs`
- Supports flexible CSV headers (all optional)
- Handles data validation and error reporting
- Updates staging database table

## Production Deployment Notes

- Configure real database in `appsettings.Production.json`
- Update password policies in `Program.cs`
- Enable HTTPS and security headers
- Set secure admin passwords
- Configure proper logging

## Troubleshooting

### Common Issues
1. **Database connection errors**: Remove connection string to use in-memory database
2. **Port conflicts**: Application uses port 5255 (HTTP) and 7293 (HTTPS)
3. **Build warnings**: 9 nullable reference warnings are expected
4. **EF tools missing**: Install with `dotnet tool install --global dotnet-ef`

### Performance Notes
- In-memory database is fastest for development
- Debug builds are slower than Release builds
- Application startup includes database seeding (adds ~5 seconds)

## File Locations

### Frequently Modified Files
- Controllers: `/Controllers/CveController.cs`, `/Controllers/AccountController.cs`
- Views: `/Views/Cve/Index.cshtml`, `/Views/Account/Login.cshtml`
- Models: `/Models/CveUpdateStaging.cs`, `/Models/ApplicationUser.cs`
- Configuration: `appsettings.Development.json`, `Program.cs`

### Generated/Build Artifacts
- `/bin/` - Compiled assemblies
- `/obj/` - Build intermediate files
- `/bin/Release/net8.0/publish/` - Published output

## Validation Checklist

Before submitting changes:
- [ ] `dotnet build` succeeds (warnings OK, errors not OK)
- [ ] Application starts without errors
- [ ] Login with both admin and user accounts works
- [ ] CVE Dashboard displays data correctly
- [ ] Admin tools accessible only to admin users
- [ ] No new console errors in browser
- [ ] Navigation and basic functionality tested

**REMEMBER**: This application has no automated tests. Manual validation through the UI is required for all changes.