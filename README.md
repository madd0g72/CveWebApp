# CVEWebApp

A web application for managing and importing CVE (Common Vulnerabilities and Exposures) data with database integration, CSV file upload functionality, and role-based access control. This project is built with ASP.NET Core MVC and uses MariaDB for data storage.

## Features

- **CVE Dashboard**: View and analyze imported CVE data in a user-friendly dashboard.
- **CSV Import**: Upload new CVE records via CSV files and update the staging database table *(Admin only)*.
- **KB Import**: Dedicated interface for importing Knowledge Base (KB) articles *(Admin only)*.
- **Role-Based Access Control**: Secure admin-only import features with ASP.NET Core Identity.
- **User Authentication**: Login/logout functionality with role-based navigation.
- **Data Validation**: Ensures imported data matches the schema and provides user feedback.
- **Responsive UI**: Built with Bootstrap for seamless desktop and mobile experience.
- **Compliance Overview**: Analyze server compliance with KB patch requirements.

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

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [MariaDB](https://mariadb.org/) or MySQL (optional - uses in-memory database by default)

### Setup

1. **Clone the repository:**

    ```bash
    git clone https://github.com/madd0g72/CVEWebApp.git
    cd CVEWebApp
    ```

2. **Configure the database connection (optional):**

    Edit `appsettings.json` and set your MariaDB connection string:

    ```json
    "ConnectionStrings": {
      "DefaultConnection": "server=localhost;database=cve_db;user=root;password=yourpassword;"
    }
    ```

    **Note**: If no connection string is provided, the application will use an in-memory database with pre-seeded test data.

3. **Run the application:**

    ```bash
    dotnet run
    ```

4. **Access the app:**

    Open your browser and go to [https://localhost:5001](https://localhost:5001)

5. **Login with demo credentials:**

    - **Admin**: `admin@cveapp.local` / `admin123`
    - **User**: `user@cveapp.local` / `user123`

## Usage

### For All Users
- **CVE Dashboard**: Navigate to the home page to view all imported CVEs
- **CVE Details**: Click on any CVE to view detailed information
- **Compliance Overview**: View server compliance status for patches

### For Admin Users Only
- **Import CVE Data**: Access via "Admin Tools" dropdown to upload new CSV files
- **KB Import**: Access via "Admin Tools" dropdown for Knowledge Base data

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

### Identity Tables
- AspNetUsers, AspNetRoles, AspNetUserRoles (standard Identity tables)

> See full schema in the `/Data` or `/Migrations` folder.

## Production Deployment

For production deployment:

1. **Configure a real database** in `appsettings.Production.json`
2. **Set secure passwords** for admin accounts
3. **Review password policies** in `Program.cs` (currently simplified for development)
4. **Configure HTTPS** and security headers
5. **Set up proper user management** workflow

## Contributing

Pull requests are welcome! For major changes, please open an issue first to discuss what you would like to change.

## License

[MIT](LICENSE)

## Acknowledgments

- [ASP.NET Core Identity](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Bootstrap](https://getbootstrap.com/)
- [MariaDB](https://mariadb.org/)
- [NVD](https://nvd.nist.gov/) for CVE data

