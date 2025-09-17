using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using CveWebApp.Models;

namespace CveWebApp.Data
{
    /// <summary>
    /// Application database context with Identity support for role-based authentication
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CveUpdateStaging> CveUpdateStagings { get; set; }
        public DbSet<ServerInstalledKb> ServerInstalledKbs { get; set; }
        public DbSet<LoginAttempt> LoginAttempts { get; set; }
        public DbSet<KbSupersedence> KbSupersedences { get; set; }
        public DbSet<Server> Servers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ServerService> ServerServices { get; set; }

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

            // Configure ServerInstalledKb entity
            modelBuilder.Entity<ServerInstalledKb>()
                .ToTable("ServerInstalledKbs");

            // Create composite index for better query performance
            modelBuilder.Entity<ServerInstalledKb>()
                .HasIndex(e => new { e.Computer, e.KB })
                .IsUnique();

            // Configure LoginAttempt entity
            modelBuilder.Entity<LoginAttempt>()
                .ToTable("LoginAttempts");

            // Create indexes for better query performance
            modelBuilder.Entity<LoginAttempt>()
                .HasIndex(e => e.Timestamp);

            modelBuilder.Entity<LoginAttempt>()
                .HasIndex(e => e.Username);

            modelBuilder.Entity<LoginAttempt>()
                .HasIndex(e => e.IsSuccess);

            // Configure KbSupersedence entity
            modelBuilder.Entity<KbSupersedence>()
                .ToTable("KbSupersedences");

            // Create indexes for better query performance on KB supersedence
            modelBuilder.Entity<KbSupersedence>()
                .HasIndex(e => e.OriginalKb);

            modelBuilder.Entity<KbSupersedence>()
                .HasIndex(e => e.SupersedingKb);

            // Create composite index for supersedence relationships
            modelBuilder.Entity<KbSupersedence>()
                .HasIndex(e => new { e.OriginalKb, e.SupersedingKb })
                .IsUnique();

            // Configure Server entity
            modelBuilder.Entity<Server>()
                .ToTable("Servers");

            // Create indexes for better query performance on servers
            modelBuilder.Entity<Server>()
                .HasIndex(e => e.ServerName)
                .IsUnique();

            modelBuilder.Entity<Server>()
                .HasIndex(e => e.Environment);

            modelBuilder.Entity<Server>()
                .HasIndex(e => e.Project);

            modelBuilder.Entity<Server>()
                .HasIndex(e => e.OperatingSystem);

            // Configure decimal precision for disk sizes
            modelBuilder.Entity<Server>()
                .Property(e => e.OSDiskSize)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Server>()
                .Property(e => e.OSDiskFree)
                .HasPrecision(18, 2);

            // Configure Service entity
            modelBuilder.Entity<Service>()
                .ToTable("Services");

            modelBuilder.Entity<Service>()
                .HasIndex(e => e.ServiceName)
                .IsUnique();

            // Configure ServerService entity (many-to-many)
            modelBuilder.Entity<ServerService>()
                .ToTable("ServerServices");

            modelBuilder.Entity<ServerService>()
                .HasIndex(e => new { e.ServerId, e.ServiceId })
                .IsUnique();

            // Configure relationships
            modelBuilder.Entity<ServerService>()
                .HasOne(ss => ss.Server)
                .WithMany(s => s.ServerServices)
                .HasForeignKey(ss => ss.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ServerService>()
                .HasOne(ss => ss.Service)
                .WithMany(s => s.ServerServices)
                .HasForeignKey(ss => ss.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship between Server and ServerInstalledKb
            modelBuilder.Entity<ServerInstalledKb>()
                .HasOne<Server>()
                .WithMany(s => s.InstalledKbs)
                .HasForeignKey(kb => kb.Computer)
                .HasPrincipalKey(s => s.ServerName)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}