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
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Production.json", optional: true) // <-- add this line!
                .Build();

            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var dbProvider = configuration["DatabaseProvider"] ?? "MariaDb";

            if (dbProvider.Equals("MariaDb", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
            else if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                builder.UseSqlServer(connectionString);
            }
            else
            {
                throw new Exception($"Unsupported DatabaseProvider: {dbProvider}");
            }

            return new ApplicationDbContext(builder.Options);
        }
    }
}