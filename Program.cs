using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CveWebApp.Data;
using CveWebApp.Models;
using QuestPDF.Infrastructure;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Detect provider from config
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "MariaDb";

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

    // Only run migrations if using a relational database provider
    if (!context.Database.IsInMemory())
    {
        // This will create the database and apply any pending migrations
        await context.Database.MigrateAsync();
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

        await SeedRolesAndUsersAsync(userManager, roleManager);
        await SeedTestDataAsync(context);
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

async Task SeedTestDataAsync(ApplicationDbContext context)
{
    if (await context.CveUpdateStagings.AnyAsync())
        return;

    var testCveRecords = new List<CveUpdateStaging>
    {
        new CveUpdateStaging
        {
            ReleaseDate = DateTime.Now.AddDays(-30),
            ProductFamily = "Windows",
            Product = "Windows 10",
            Platform = "x64",
            Impact = "Critical",
            MaxSeverity = "Critical",
            Article = "KB5001234",
            Details = "CVE-2024-12345, CVE-2024-54321",
            BaseScore = 9.8m,
            CustomerActionRequired = true
        },
        new CveUpdateStaging
        {
            ReleaseDate = DateTime.Now.AddDays(-15),
            ProductFamily = "Windows",
            Product = "Windows 11",
            Platform = "x64",
            Impact = "High",
            MaxSeverity = "High",
            Article = "KB5002345",
            Details = "CVE-2024-99999",
            BaseScore = 7.5m,
            CustomerActionRequired = false
        },
        new CveUpdateStaging
        {
            ReleaseDate = DateTime.Now.AddDays(-7),
            ProductFamily = "Office",
            Product = "Microsoft Office 2019",
            Platform = "x86",
            Impact = "Medium",
            MaxSeverity = "Medium",
            Article = "KB5003456",
            Details = "CVE-2024-11111, CVE-2024-22222",
            BaseScore = 5.2m,
            CustomerActionRequired = true
        },
        new CveUpdateStaging
        {
            ReleaseDate = DateTime.Now.AddDays(-45),
            ProductFamily = "Windows",
            Product = "Windows Server 2019",
            Platform = "x64",
            Impact = "Low",
            MaxSeverity = "Low",
            Article = "KB5004567",
            Details = "CVE-2024-88888",
            BaseScore = 3.1m,
            CustomerActionRequired = false
        },
        new CveUpdateStaging
        {
            ReleaseDate = DateTime.Now.AddDays(-60),
            ProductFamily = "Office",
            Product = "Microsoft Office 2021",
            Platform = "x64",
            Impact = "High",
            MaxSeverity = "High",
            Article = "KB5005678",
            Details = "CVE-2024-77777",
            BaseScore = 8.2m,
            CustomerActionRequired = true
        }
    };

    context.CveUpdateStagings.AddRange(testCveRecords);

    // Add some test server data to demonstrate actual compliance calculations
    var testServerData = new List<ServerInstalledKb>
    {
        // Windows 10 servers with some KBs installed
        new ServerInstalledKb { Computer = "WIN10-SRV-01", OSProduct = "Windows 10 Enterprise", KB = "5001234" },
        new ServerInstalledKb { Computer = "WIN10-SRV-01", OSProduct = "Windows 10 Enterprise", KB = "5000123" },
        new ServerInstalledKb { Computer = "WIN10-SRV-02", OSProduct = "Windows 10 Pro", KB = "5000456" },
        
        // Windows 11 servers
        new ServerInstalledKb { Computer = "WIN11-SRV-01", OSProduct = "Windows 11 Enterprise", KB = "5002345" },
        new ServerInstalledKb { Computer = "WIN11-SRV-01", OSProduct = "Windows 11 Enterprise", KB = "5001000" },
        new ServerInstalledKb { Computer = "WIN11-SRV-02", OSProduct = "Windows 11 Pro", KB = "5001001" },
        
        // Office servers
        new ServerInstalledKb { Computer = "OFFICE-SRV-01", OSProduct = "Microsoft Office 2019", KB = "5003456" },
        new ServerInstalledKb { Computer = "OFFICE-SRV-02", OSProduct = "Microsoft Office 2019", KB = "5003000" },
        
        // Windows Server 2019
        new ServerInstalledKb { Computer = "WS2019-SRV-01", OSProduct = "Windows Server 2019", KB = "5004567" },
        new ServerInstalledKb { Computer = "WS2019-SRV-02", OSProduct = "Windows Server 2019", KB = "5004567" },
        
        // Office 2021 servers
        new ServerInstalledKb { Computer = "O365-SRV-01", OSProduct = "Microsoft Office 2021", KB = "5005678" },
        new ServerInstalledKb { Computer = "O365-SRV-02", OSProduct = "Microsoft Office 2021", KB = "5005000" }
    };

    context.ServerInstalledKbs.AddRange(testServerData);
    await context.SaveChangesAsync();
}