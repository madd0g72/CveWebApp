using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Identity;
using CveWebApp.Data;
using CveWebApp.Models;
using CveWebApp.Services;
using QuestPDF.Infrastructure;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register the CSV data loader service
builder.Services.AddScoped<CsvDataLoader>();

// Detect provider from config with environment-specific defaults
var dbProvider = builder.Configuration["DatabaseProvider"];
if (string.IsNullOrEmpty(dbProvider))
{
    // Set environment-specific defaults if not explicitly configured
    dbProvider = builder.Environment.IsDevelopment() ? "MariaDb" : "SqlServer";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        if (dbProvider.Equals("MariaDb", StringComparison.OrdinalIgnoreCase))
        {
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(10, 5, 0)),
                mysqlOptions => mysqlOptions.EnableRetryOnFailure().CommandTimeout(30)
            );
        }
        else if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure()
            );
        }
        else
        {
            throw new Exception($"Unsupported DatabaseProvider: {dbProvider}");
        }
    }
    else
    {
        // Use in-memory DB for testing/demo if no connection string is given
        options.UseInMemoryDatabase("TestDatabase");
    }
});

// Add Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
});

var app = builder.Build();

// ----- Database migration and seeding -----
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    
    // Only run migrations if using a relational database provider
    if (!context.Database.IsInMemory())
    {
        // Validate environment and database provider alignment for relational databases
        var currentProvider = context.Database.ProviderName;
        var expectedProvider = environment.IsDevelopment() ? "Pomelo.EntityFrameworkCore.MySql" : "Microsoft.EntityFrameworkCore.SqlServer";
        
        if (currentProvider != null && !currentProvider.Contains(expectedProvider.Split('.').Last()))
        {
            var env = environment.IsDevelopment() ? "Development" : "Production";
            var expected = environment.IsDevelopment() ? "MariaDb/MySQL" : "SQL Server";
            throw new InvalidOperationException(
                $"Database provider mismatch! {env} environment expects {expected} but got {currentProvider}. " +
                "Check your appsettings configuration.");
        }
        
        // Check if database exists and has tables
        var canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            // Check if any tables exist (look for AspNetUsers as it's always created)
            var hasSchema = await context.Database.GetService<IRelationalDatabaseCreator>().HasTablesAsync();
            
            if (!hasSchema)
            {
                // Database exists but is empty - create schema using EnsureCreated for cross-provider compatibility
                await context.Database.EnsureCreatedAsync();
            }
            else
            {
                // Database has tables - apply pending migrations only if using MySQL (migrations are MySQL-specific)
                if (currentProvider != null && currentProvider.Contains("MySql"))
                {
                    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        await context.Database.MigrateAsync();
                    }
                }
                // For SQL Server with existing schema, skip migrations as they contain MySQL-specific code
            }
        }
        else
        {
            // Database doesn't exist - create it
            if (currentProvider != null && currentProvider.Contains("MySql"))
            {
                // For MySQL, use migrations
                await context.Database.MigrateAsync();
            }
            else
            {
                // For SQL Server, use EnsureCreated to avoid MySQL-specific migration issues
                await context.Database.EnsureCreatedAsync();
            }
        }
    }
    else
    {
        // For in-memory database, just ensure it's created
        await context.Database.EnsureCreatedAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var csvDataLoader = scope.ServiceProvider.GetRequiredService<CsvDataLoader>();

        await SeedRolesAndUsersAsync(userManager, roleManager);
        await SeedRealDataAsync(context, csvDataLoader, app.Environment);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>
/// Seeds application roles and creates default admin user for testing/development
/// </summary>
async Task SeedRolesAndUsersAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
{
    string[] roleNames = { "Admin", "User", "operator" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var adminEmail = "admin@cveapp.local";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator"
        };

        var result = await userManager.CreateAsync(adminUser, "admin123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    var userEmail = "user@cveapp.local";
    var testUser = await userManager.FindByEmailAsync(userEmail);

    if (testUser == null)
    {
        testUser = new ApplicationUser
        {
            UserName = userEmail,
            Email = userEmail,
            EmailConfirmed = true,
            FullName = "Test User"
        };

        var result = await userManager.CreateAsync(testUser, "user123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(testUser, "User");
        }
    }
}

async Task SeedRealDataAsync(ApplicationDbContext context, CsvDataLoader csvDataLoader, IWebHostEnvironment environment)
{
    // Only load real data in development if no data exists yet
    if (await context.CveUpdateStagings.AnyAsync())
        return;

    var dataPath = Path.Combine(environment.ContentRootPath, "Data");
    var msrcCsvPath = Path.Combine(dataPath, "MSRC_SecurityUpdates_2025_v3.csv");
    var wsusCsvPath = Path.Combine(dataPath, "WSUS_InstalledKBs.csv");

    try
    {
        // Load MSRC security updates data
        await csvDataLoader.LoadMsrcSecurityUpdatesAsync(msrcCsvPath);
        
        // Load WSUS installed KBs data
        await csvDataLoader.LoadWsusInstalledKbsAsync(wsusCsvPath);
        
        // Process supersedence relationships from the loaded data
        await csvDataLoader.ProcessSupersedenceRelationshipsAsync();
    }
    catch (Exception ex)
    {
        // Log error but don't fail the application startup
        Console.WriteLine($"Error loading CSV data: {ex.Message}");
        
        // Fall back to minimal test data if CSV loading fails
        await SeedMinimalTestDataAsync(context);
    }
}

async Task SeedMinimalTestDataAsync(ApplicationDbContext context)
{
    // Minimal fallback data in case CSV loading fails
    var fallbackCveRecord = new CveUpdateStaging
    {
        ReleaseDate = DateTime.Now.AddDays(-30),
        ProductFamily = "Windows",
        Product = "Windows Server 2016",
        Platform = "x64",
        Impact = "Critical",
        MaxSeverity = "Critical",
        Article = "KB5058383",
        Details = "CVE-2025-29833",
        BaseScore = 7.7m,
        CustomerActionRequired = true,
        Supercedence = "5055521"
    };

    context.CveUpdateStagings.Add(fallbackCveRecord);

    // Add our test server
    var fallbackServerData = new List<ServerInstalledKb>
    {
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "4052623" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "5065427" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "2267602" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "5012170" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "925673" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "5065687" }
    };

    context.ServerInstalledKbs.AddRange(fallbackServerData);

    await context.SaveChangesAsync();
}