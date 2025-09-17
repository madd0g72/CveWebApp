# CVEWebApp

A web application for managing and importing CVE (Common Vulnerabilities and Exposures) data with database integration, CSV file upload functionality, and role-based access control. This project is built with ASP.NET Core MVC and uses MariaDB for data storage.

## Features

- **CVE Dashboard**: View and analyze imported CVE data in a user-friendly dashboard.
- **CSV Import**: Upload new CVE records via CSV files and update the staging database table *(Admin only)*.
- **KB Import**: Dedicated interface for importing Knowledge Base (KB) articles *(Admin only)*.
- **KB Supersedence Management**: Advanced compliance tracking that considers Knowledge Base supersedence relationships *(Admin only)*.
- **Role-Based Access Control**: Secure admin-only import features with ASP.NET Core Identity.
- **User Authentication**: Login/logout functionality with role-based navigation.
- **Data Validation**: Ensures imported data matches the schema and provides user feedback.
- **Responsive UI**: Built with Bootstrap for seamless desktop and mobile experience.
- **Compliance Overview**: Analyze server compliance with KB patch requirements, including supersedence logic.

## Security & Authentication

The application implements role-based security using ASP.NET Core Identity:

### User Roles
- **Admin**: Full access to all features including CVE and KB import functionality
- **User**: Read-only access to CVE dashboard, details, and compliance views

### Demo Credentials
For development and testing purposes:
- **Admin User**: `admin@cveapp.local` / `admin123`
- **Regular User**: `user@cveapp.local` / `user123`

### Security Features
- Import functionality is restricted to users with Admin role
- Unauthorized access attempts are redirected to an Access Denied page
- Navigation dynamically shows/hides admin features based on user role
- Secure logout functionality
- Password validation and user management

## Screenshots

> Replace the below image paths with real images in your repository, e.g. `/wwwroot/images/dashboard.png`.  
> If you add screenshots, store them in `wwwroot/images/` and update the paths below.

![CVE Dashboard](wwwroot/images/dashboard-sample.png)
*Dashboard view showing latest imported CVEs.*

![CSV Import Page](wwwroot/images/import-sample.png)
*Import page for uploading and validating CVE CSV files.*

## Getting Started

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- **Development**: [MariaDB](https://mariadb.org/) or MySQL (optional - uses in-memory database by default)  
- **Production**: [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (required)

### Environment Setup

> **Important**: This application uses different database providers for different environments:
> - **Development**: MariaDB/MySQL (with in-memory fallback for testing)
> - **Production**: SQL Server Express
> 
> See [ENVIRONMENT_SETUP.md](ENVIRONMENT_SETUP.md) for detailed configuration instructions.

### Quick Start

#### Development (In-Memory Database)
```bash
git clone https://github.com/madd0g72/CVEWebApp.git
cd CVEWebApp
dotnet run --environment Development
```

#### Development (With MariaDB)
```bash
# 1. Install and start MariaDB/MySQL
# 2. Update appsettings.Development.json with your connection string
# 3. Run the application
dotnet run --environment Development
```

#### Production (SQL Server Express)
```bash
# 1. Install SQL Server Express
# 2. Update appsettings.Production.json with your connection string  
# 3. Run the application
dotnet run --environment Production
```

### Default Login Credentials

#### Development Environment
- **Admin**: `admin@cveapp.local` / `admin123`
- **User**: `user@cveapp.local` / `user123`

> **Note**: Test accounts are only created in Development environment.

#### Production Environment
- **Default Admin**: `admin@company.local` / `AdminPass1!`

> **⚠️ CRITICAL**: Change the default admin password immediately after first deployment!
> 
> See [PRODUCTION_ADMIN_SETUP.md](PRODUCTION_ADMIN_SETUP.md) for detailed setup instructions.

## Usage

### For All Users
- **CVE Dashboard**: Navigate to the home page to view all imported CVEs
- **CVE Details**: Click on any CVE to view detailed information
- **Compliance Overview**: View server compliance status for patches

### For Admin Users Only
- **Import CVE Data**: Access via "Admin Tools" dropdown to upload new CSV files
- **KB Import**: Access via "Admin Tools" dropdown for Knowledge Base data  
- **KB Supersedence**: Access via "Admin Tools" dropdown to view and manage KB supersedence relationships

### KB Supersedence Feature
The application includes advanced KB supersedence functionality that improves compliance accuracy:

- **Automatic Processing**: When CVE data is imported, supersedence relationships are automatically extracted from the "Supercedence" field
- **Manual Processing**: Admins can manually trigger supersedence data processing for existing CVE records
- **Enhanced Compliance**: Servers with newer KBs that supersede required KBs are automatically marked as compliant
- **Relationship Management**: View all supersedence relationships in a dedicated admin interface

**Example**: If a CVE requires KB5001234 but a server has KB5000456, and KB5000456 supersedes KB5001234, the server will be marked as compliant even without the exact KB mentioned in the CVE.

### Access Control
- Admin features are only visible in navigation when logged in as an admin user
- Direct URL access to admin pages is blocked for non-admin users
- Clear access denied messages guide users to appropriate resources

## Database Schema

The application uses Entity Framework Core with the following main entities:

### CveUpdateStaging Table
- Id (Primary Key, auto-increment)
- ReleaseDate, ProductFamily, Product, Platform, Impact, MaxSeverity
- Article, ArticleLink, Supercedence, Download, DownloadLink, BuildNumber
- Details, DetailsLink, BaseScore, TemporalScore, CustomerActionRequired

### ServerInstalledKb Table
- Computer, OSProduct, KB, LastUpdated

### KbSupersedence Table  
- OriginalKb, SupersedingKb, Product, ProductFamily, DateAdded
- Stores relationships where newer KBs replace older ones for compliance checking

### Identity Tables
- AspNetUsers, AspNetRoles, AspNetUserRoles (standard Identity tables)

> See full schema in the `/Data` or `/Migrations` folder.

## Production Deployment

For production deployment, see [ENVIRONMENT_SETUP.md](ENVIRONMENT_SETUP.md) for detailed instructions.

**Key Requirements for Production:**

1. **Install SQL Server Express** - Required database provider for production
2. **Configure secure connection string** in `appsettings.Production.json`
3. **Admin User Setup** - Default admin created automatically on first run
4. **Change Default Password** - See [PRODUCTION_ADMIN_SETUP.md](PRODUCTION_ADMIN_SETUP.md) for admin setup
5. **Set Environment Variable** - `ASPNETCORE_ENVIRONMENT=Production`
6. **Self-Service Password Reset** - No email configuration required

**⚠️ Production Security Notes:**
- Default admin user (`admin@company.local`) created with password `AdminPass1!`
- **Change the default password immediately** after deployment
- Production environment enforces strong password policies
- Self-service password reset available for users without email dependency

**Key Requirements for Production:**

1. **Install SQL Server Express** - Required database provider for production
2. **Configure secure connection string** in `appsettings.Production.json`
3. **Set secure passwords** for admin accounts (no test accounts in production)
4. **Review password policies** in `Program.cs` (currently simplified for development)
5. **Configure HTTPS** and security headers
6. **Set up proper user management** workflow
7. **Use environment variable** `ASPNETCORE_ENVIRONMENT=Production`

**Environment Separation:**
- ✅ **Development**: Uses MariaDB/MySQL (or in-memory for testing)
- ✅ **Production**: Uses SQL Server Express  
- ✅ **Auto-validation**: Prevents database provider mismatches
- ✅ **Idempotent migrations**: Safe database setup on first startup

## Contributing

Pull requests are welcome! For major changes, please open an issue first to discuss what you would like to change.

## License

[MIT](LICENSE)

## Acknowledgments

- [ASP.NET Core Identity](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Bootstrap](https://getbootstrap.com/)
- [MariaDB](https://mariadb.org/)
- [NVD](https://nvd.nist.gov/) for CVE data

