using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace CveWebApp.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Determine environment from environment variable or command line args
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            
            // Build configuration with environment-specific settings
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var dbProvider = configuration["DatabaseProvider"];
            
            // Set environment-specific defaults if not explicitly configured
            if (string.IsNullOrEmpty(dbProvider))
            {
                dbProvider = environment.Equals("Development", StringComparison.OrdinalIgnoreCase) ? "InMemory" : "SqlServer";
            }

            // For development, always use in-memory database
            if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseInMemoryDatabase("DesignTimeDatabase");
                return new ApplicationDbContext(builder.Options);
            }

            // Validate that we have a connection string for relational databases
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"No connection string found for environment '{environment}'. " +
                    $"Please configure 'DefaultConnection' in appsettings.{environment}.json");
            }

            if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseSqlServer(connectionString);
            }
            else
            {
                throw new Exception($"Unsupported DatabaseProvider: {dbProvider}. Supported providers: SqlServer");
            }

            return new ApplicationDbContext(builder.Options);
        }
    }
}