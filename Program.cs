using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Entity Framework with MariaDB
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(10, 5, 0)), // MariaDB 10.5+
            mysqlOptions => mysqlOptions
                .EnableRetryOnFailure()
                .CommandTimeout(30)
        );
    }
    else
    {
        // Use in-memory database for testing/demo when no connection string is available
        options.UseInMemoryDatabase("TestDatabase");
    }
});

var app = builder.Build();

// Seed test data in development mode
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedTestDataAsync(context);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

async Task SeedTestDataAsync(ApplicationDbContext context)
{
    // Check if we already have data
    if (await context.CveUpdateStagings.AnyAsync())
    {
        return; // Data already seeded
    }

    var testCveRecords = new List<CveUpdateStaging>
    {
        new CveUpdateStaging
        {
            Id = 1,
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
            Id = 2,
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
            Id = 3,
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
        }
    };

    context.CveUpdateStagings.AddRange(testCveRecords);
    await context.SaveChangesAsync();
}
