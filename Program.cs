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

// Configure logging to include file-based application logging
builder.Logging.ClearProviders(); // Clear default providers
builder.Logging.AddConsole(); // Add console logging back
builder.Logging.AddDebug(); // Add debug logging back

// Add custom file logger provider for framework and application logs
if (builder.Configuration.GetValue<bool>("FileLogging:Enabled", false) && 
    builder.Configuration.GetValue<bool>("FileLogging:ApplicationLoggingEnabled", false))
{
    builder.Logging.AddProvider(new CveWebApp.Services.FileLoggerProvider(builder.Configuration, builder.Environment));
}

// Configure specific log levels for different categories
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Information);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add file logging service
builder.Services.AddScoped<CveWebApp.Services.IFileLoggingService, CveWebApp.Services.FileLoggingService>();

// Configure Active Directory settings
builder.Services.Configure<ActiveDirectorySettings>(
    builder.Configuration.GetSection("ActiveDirectory"));

// Add Active Directory services
builder.Services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();

// Detect provider from config with environment-specific defaults
var dbProvider = builder.Configuration["DatabaseProvider"];
if (string.IsNullOrEmpty(dbProvider))
{
    // Set environment-specific defaults if not explicitly configured
    dbProvider = builder.Environment.IsDevelopment() ? "InMemory" : "SqlServer";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // For development, always use in-memory database regardless of connection string
    if (builder.Environment.IsDevelopment())
    {
        options.UseInMemoryDatabase("TestDatabase");
    }
    else if (!string.IsNullOrEmpty(connectionString))
    {
        if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
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

    // Enable detailed Entity Framework logging
    if (builder.Configuration.GetValue<bool>("FileLogging:ApplicationLoggingEnabled", false))
    {
        options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
        options.EnableDetailedErrors(builder.Environment.IsDevelopment());
        options.LogTo(message => 
        {
            // This will be captured by our custom logger provider
            var logger = LoggerFactory.Create(config => 
            {
                if (builder.Configuration.GetValue<bool>("FileLogging:Enabled", false))
                {
                    config.AddProvider(new CveWebApp.Services.FileLoggerProvider(builder.Configuration, builder.Environment));
                }
            }).CreateLogger("EntityFramework");
            logger.LogInformation(message);
        }, LogLevel.Information);
    }
});

// Add Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Environment-specific password policies
    if (builder.Environment.IsDevelopment())
    {
        // Relaxed policies for development
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 4;
    }
    else
    {
        // Secure policies for production
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
    }
    
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
    
    // Validate environment and database provider alignment for relational databases
    if (!context.Database.IsInMemory())
    {
        var currentProvider = context.Database.ProviderName;
        var expectedProvider = "Microsoft.EntityFrameworkCore.SqlServer";
        
        if (currentProvider != null && !currentProvider.Contains(expectedProvider.Split('.').Last()))
        {
            var expected = "SQL Server";
            throw new InvalidOperationException(
                $"Database provider mismatch! Production environment expects {expected} but got {currentProvider}. " +
                "Check your appsettings configuration.");
        }
    }
    
    // Initialize database with proper schema handling for missing columns
    await CveWebApp.Data.DatabaseInitializer.InitializeDatabaseAsync(context);

    // Always seed roles and admin user in all environments
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    await SeedRolesAndUsersAsync(userManager, roleManager);
    
    if (app.Environment.IsDevelopment())
    {
        // Seed basic services for development
        await SeedServicesAsync(context);
        // Removed automatic CSV data loading and test data seeding
        // All data population must now happen via admin import functionality
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
/// Seeds application roles and creates default admin user for all environments
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

    // Create default admin user for all environments
    var adminEmail = app.Environment.IsDevelopment() ? "admin@cveapp.local" : "admin@company.local";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = app.Environment.IsDevelopment() ? "System Administrator" : "Production Administrator"
        };

        // Use different default passwords for different environments
        var adminPassword = app.Environment.IsDevelopment() ? "admin123" : "AdminPass1!";
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            
            // Log admin creation for production environments
            if (!app.Environment.IsDevelopment())
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Default admin user created: {AdminEmail}. Please change the password immediately!", adminEmail);
            }
        }
    }

    // Only create test user in development
    if (app.Environment.IsDevelopment())
    {
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
}

/// <summary>
/// Seeds basic services for development environments
/// </summary>
async Task SeedServicesAsync(ApplicationDbContext context)
{
    if (!context.Services.Any())
    {
        var services = new[]
        {
            new Service { ServiceName = "SQL Server", Description = "Microsoft SQL Server Database Engine", Vendor = "Microsoft" },
            new Service { ServiceName = "Cynet", Description = "Cynet Security Platform", Vendor = "Cynet" },
            new Service { ServiceName = "Wincollect", Description = "IBM QRadar WinCollect Agent", Vendor = "IBM" },
            new Service { ServiceName = "SentinelOne", Description = "SentinelOne Endpoint Protection", Vendor = "SentinelOne" }
        };

        await context.Services.AddRangeAsync(services);
        await context.SaveChangesAsync();
    }
}