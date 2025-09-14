using Microsoft.EntityFrameworkCore;
using CveWebApp.Models;

namespace CveWebApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CveUpdateStaging> CveUpdateStagings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure decimal precision for scores
            modelBuilder.Entity<CveUpdateStaging>()
                .Property(e => e.BaseScore)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CveUpdateStaging>()
                .Property(e => e.TemporalScore)
                .HasPrecision(18, 2);

            // Configure table name explicitly
            modelBuilder.Entity<CveUpdateStaging>()
                .ToTable("Staging");
        }
    }
}