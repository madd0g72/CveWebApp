# CVEWebApp

A web application for managing and importing CVE (Common Vulnerabilities and Exposures) data with database integration and CSV file upload functionality. This project is built with ASP.NET Core MVC and uses MariaDB for data storage.

## Features

- **CVE Dashboard**: View and analyze imported CVE data in a user-friendly dashboard.
- **CSV Import**: Upload new CVE records via CSV files and update the staging database table.
- **KB Import**: Dedicated interface for importing Knowledge Base (KB) articles.
- **Data Validation**: Ensures imported data matches the schema and provides user feedback.
- **Role-based Access**: Only authorized users can import data.
- **Responsive UI**: Built with Bootstrap for seamless desktop and mobile experience.

## Screenshots

> Replace the below image paths with real images in your repository, e.g. `/wwwroot/images/dashboard.png`.  
> If you add screenshots, store them in `wwwroot/images/` and update the paths below.

![CVE Dashboard](wwwroot/images/dashboard-sample.png)
*Dashboard view showing latest imported CVEs.*

![CSV Import Page](wwwroot/images/import-sample.png)
*Import page for uploading and validating CVE CSV files.*

## Getting Started

### Prerequisites

- [.NET 6 SDK](https://dotnet.microsoft.com/download)
- [MariaDB](https://mariadb.org/) or MySQL

### Setup

1. **Clone the repository:**

    ```bash
    git clone https://github.com/madd0g72/CVEWebApp.git
    cd CVEWebApp
    ```

2. **Configure the database connection:**

    Edit `appsettings.json` and set your MariaDB connection string:

    ```json
    "ConnectionStrings": {
      "DefaultConnection": "server=localhost;database=cve_db;user=root;password=yourpassword;"
    }
    ```

3. **Apply migrations:**

    ```bash
    dotnet ef database update
    ```

4. **Run the application:**

    ```bash
    dotnet run
    ```

5. **Access the app:**

    Open your browser and go to [https://localhost:5001](https://localhost:5001)

## Usage

- **CVE Dashboard**: Navigate to the home page to view all imported CVEs.
- **Import CVE Data**: Click on the "Import CVE Data" menu to upload new CSV files.
- **KB Import**: Use the "KB Import" menu for Knowledge Base data.

## Database Schema

The `staging` table includes:

- Id (Primary Key, auto-increment)
- ReleaseDate, ProductFamily, Product, Platform, Impact, MaxSeverity
- Article, ArticleLink, Supercedence, Download, DownloadLink, BuildNumber
- Details, DetailsLink, BaseScore, TemporalScore, CustomerActionRequired

> See full schema in the `/Data` or `/Migrations` folder.

## Contributing

Pull requests are welcome! For major changes, please open an issue first to discuss what you would like to change.

## License

[MIT](LICENSE)

## Acknowledgments

- [Bootstrap](https://getbootstrap.com/)
- [MariaDB](https://mariadb.org/)
- [NVD](https://nvd.nist.gov/) for CVE data

