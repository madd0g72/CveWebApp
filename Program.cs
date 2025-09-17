using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Identity;
using CveWebApp.Data;
using CveWebApp.Models;
using QuestPDF.Infrastructure;

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

    try
    {
        // Load real CVE data from CSV file
        await LoadCveDataFromCsvAsync(context);
        
        // Load real server KB data from CSV file
        await LoadServerKbDataFromCsvAsync(context);
        
        // Process and build supersedence relationships from real data
        await ProcessSupersedenceDataFromCsvAsync(context);
        
        await context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        // If loading real data fails, fall back to minimal test data for development
        Console.WriteLine($"Failed to load real CSV data: {ex.Message}. Using minimal test data.");
        await LoadMinimalTestDataAsync(context);
    }
}

async Task LoadCveDataFromCsvAsync(ApplicationDbContext context)
{
    var csvPath = Path.Combine("Data", "MSRC_SecurityUpdates_2025_v3.csv");
    if (!File.Exists(csvPath))
        throw new FileNotFoundException($"CVE CSV file not found: {csvPath}");

    var lines = await File.ReadAllLinesAsync(csvPath);
    if (lines.Length < 2) return; // No data to process
    
    var records = new List<CveUpdateStaging>();
    var headers = lines[0].Split(',');
    
    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;
        
        try
        {
            var values = ParseCsvLine(lines[i]);
            if (values.Length != headers.Length) continue;
            
            var record = new CveUpdateStaging();
            
            for (int j = 0; j < headers.Length; j++)
            {
                var header = headers[j].Trim().ToLowerInvariant();
                var value = values[j].Trim();
                
                switch (header)
                {
                    case "release date":
                        if (DateTime.TryParse(value, out var date))
                            record.ReleaseDate = date;
                        break;
                    case "product family":
                        record.ProductFamily = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "product":
                        record.Product = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "platform":
                        record.Platform = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "impact":
                        record.Impact = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "max severity":
                        record.MaxSeverity = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "article":
                        record.Article = string.IsNullOrEmpty(value) ? null : $"KB{value}";
                        break;
                    case "article (link)":
                        record.ArticleLink = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "supercedence":
                        record.Supercedence = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "download":
                        record.Download = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "download (link)":
                        record.DownloadLink = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "build number":
                        record.BuildNumber = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "details":
                        record.Details = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "details (link)":
                        record.DetailsLink = string.IsNullOrEmpty(value) ? null : value;
                        break;
                    case "base score":
                        if (decimal.TryParse(value, out var baseScore))
                            record.BaseScore = baseScore;
                        break;
                    case "temporal score":
                        if (decimal.TryParse(value, out var tempScore))
                            record.TemporalScore = tempScore;
                        break;
                    case "customer action required":
                        if (bool.TryParse(value, out var required))
                            record.CustomerActionRequired = required;
                        break;
                }
            }
            
            records.Add(record);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing CVE line {i + 1}: {ex.Message}");
        }
    }
    
    if (records.Count > 0)
    {
        context.CveUpdateStagings.AddRange(records);
        Console.WriteLine($"Loaded {records.Count} CVE records from real data");
    }
}

async Task LoadServerKbDataFromCsvAsync(ApplicationDbContext context)
{
    var csvPath = Path.Combine("Data", "WSUS_InstalledKBs.csv");
    if (!File.Exists(csvPath))
        throw new FileNotFoundException($"Server KB CSV file not found: {csvPath}");

    var lines = await File.ReadAllLinesAsync(csvPath);
    if (lines.Length < 2) return; // No data to process
    
    var records = new List<ServerInstalledKb>();
    
    for (int i = 1; i < lines.Length; i++) // Skip header
    {
        if (string.IsNullOrWhiteSpace(lines[i])) continue;
        
        try
        {
            var values = ParseCsvLine(lines[i]);
            if (values.Length < 3) continue;
            
            var computer = values[0].Trim().Trim('"');
            var osProduct = values[1].Trim().Trim('"');
            var installedKbs = values[2].Trim().Trim('"');
            
            if (string.IsNullOrEmpty(computer) || string.IsNullOrEmpty(osProduct)) continue;
            
            // Parse comma-separated KB numbers
            if (!string.IsNullOrEmpty(installedKbs))
            {
                var kbNumbers = installedKbs.Split(',')
                    .Select(kb => kb.Trim())
                    .Where(kb => !string.IsNullOrEmpty(kb));
                
                foreach (var kb in kbNumbers)
                {
                    records.Add(new ServerInstalledKb
                    {
                        Computer = computer,
                        OSProduct = osProduct,
                        KB = kb.StartsWith("KB") ? kb : $"KB{kb}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing server KB line {i + 1}: {ex.Message}");
        }
    }
    
    if (records.Count > 0)
    {
        context.ServerInstalledKbs.AddRange(records);
        Console.WriteLine($"Loaded {records.Count} server KB records from real data");
    }
}

async Task ProcessSupersedenceDataFromCsvAsync(ApplicationDbContext context)
{
    // Clear existing supersedence data to rebuild with correct logic
    context.KbSupersedences.RemoveRange(context.KbSupersedences);
    
    var supersedenceRecords = new List<KbSupersedence>();
    var processedPairs = new HashSet<string>();
    
    // Get all CVE records with supersedence information
    var cveRecords = await context.CveUpdateStagings
        .Where(c => !string.IsNullOrEmpty(c.Supercedence))
        .ToListAsync();
    
    foreach (var cveRecord in cveRecords)
    {
        if (string.IsNullOrEmpty(cveRecord.Article) || string.IsNullOrEmpty(cveRecord.Supercedence))
            continue;
        
        var currentKb = NormalizeKb(cveRecord.Article);
        var supersededKb = NormalizeKb(cveRecord.Supercedence);
        
        if (string.IsNullOrEmpty(currentKb) || string.IsNullOrEmpty(supersededKb))
            continue;
        
        var pairKey = $"{supersededKb}=>{currentKb}";
        if (processedPairs.Contains(pairKey))
            continue;
        
        supersedenceRecords.Add(new KbSupersedence
        {
            OriginalKb = supersededKb,        // The KB being superseded
            SupersedingKb = currentKb,        // The KB doing the superseding
            Product = cveRecord.Product,
            ProductFamily = cveRecord.ProductFamily,
            DateAdded = DateTime.UtcNow
        });
        
        processedPairs.Add(pairKey);
    }
    
    if (supersedenceRecords.Count > 0)
    {
        context.KbSupersedences.AddRange(supersedenceRecords);
        Console.WriteLine($"Created {supersedenceRecords.Count} supersedence relationships from real data");
    }
}

async Task LoadMinimalTestDataAsync(ApplicationDbContext context)
{
    // Minimal test data focusing on CVE-2025-29833 scenario
    var testCveRecord = new CveUpdateStaging
    {
        ReleaseDate = DateTime.Parse("2025-05-13"),
        ProductFamily = "Windows",
        Product = "Windows Server 2016",
        Platform = "",
        Impact = "Remote Code Execution",
        MaxSeverity = "Critical",
        Article = "KB5058383",
        Supercedence = "KB5055521",
        Details = "CVE-2025-29833",
        BaseScore = 7.7m,
        CustomerActionRequired = true
    };
    
    context.CveUpdateStagings.Add(testCveRecord);
    
    // Add the specific server from requirements
    var serverKbs = new List<ServerInstalledKb>
    {
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "KB4052623" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "KB5065427" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "KB2267602" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "KB5012170" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "KB925673" },
        new ServerInstalledKb { Computer = "wc-docprot-fe1.lottomatica.net", OSProduct = "Windows Server 2016 Datacenter", KB = "KB5065687" }
    };
    
    context.ServerInstalledKbs.AddRange(serverKbs);
    
    // Minimal supersedence chain for the test scenario
    var testSupersedence = new KbSupersedence
    {
        OriginalKb = "KB5058383",
        SupersedingKb = "KB5065427",
        Product = "Windows Server 2016",
        ProductFamily = "Windows",
        DateAdded = DateTime.UtcNow
    };
    
    context.KbSupersedences.Add(testSupersedence);
    await context.SaveChangesAsync();
}

string[] ParseCsvLine(string line)
{
    var result = new List<string>();
    var inQuotes = false;
    var currentField = new System.Text.StringBuilder();
    
    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        
        if (c == '"')
        {
            inQuotes = !inQuotes;
        }
        else if (c == ',' && !inQuotes)
        {
            result.Add(currentField.ToString());
            currentField.Clear();
        }
        else
        {
            currentField.Append(c);
        }
    }
    
    result.Add(currentField.ToString());
    return result.ToArray();
}

string NormalizeKb(string kb)
{
    if (string.IsNullOrEmpty(kb)) return "";
    
    kb = kb.Trim();
    if (kb.StartsWith("KB", StringComparison.OrdinalIgnoreCase))
        return kb.ToUpper();
    
    if (int.TryParse(kb, out _))
        return $"KB{kb}";
    
    return "";
}