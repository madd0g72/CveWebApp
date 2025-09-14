using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CveWebApp.Data
{
    /// <summary>
    /// Factory for creating ApplicationDbContext at design time for migrations
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            
            // Use in-memory database for design-time operations
            optionsBuilder.UseInMemoryDatabase("DesignTimeDatabase");
            
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}