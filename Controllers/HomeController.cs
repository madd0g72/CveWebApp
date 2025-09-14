using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Models;
using CveWebApp.Data;

namespace CveWebApp.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public IActionResult Index(string? searchMessage = null)
    {
        ViewBag.SearchMessage = searchMessage;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchByCve(string cveId)
    {
        if (string.IsNullOrWhiteSpace(cveId))
        {
            return RedirectToAction("Index", new { searchMessage = "Please enter a CVE ID to search." });
        }

        // Clean up the CVE ID input (remove extra spaces, make case-insensitive search)
        var cleanCveId = cveId.Trim();

        try
        {
            // Search for ALL CVE records in the Details column
            var cveRecords = await _context.CveUpdateStagings
                .Where(c => c.Details != null && c.Details.Contains(cleanCveId))
                .ToListAsync();

            if (cveRecords.Any())
            {
                // Redirect to the search results page showing all affected products
                return RedirectToAction("SearchResults", new { cveId = cleanCveId });
            }
            else
            {
                // No matching CVE found
                var notFoundMessage = $"No CVE was found with ID '{cleanCveId}'. Please check the CVE ID and try again.";
                return RedirectToAction("Index", new { searchMessage = notFoundMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for CVE ID: {CveId}", cleanCveId);
            var errorMessage = "An error occurred while searching. Please try again.";
            return RedirectToAction("Index", new { searchMessage = errorMessage });
        }
    }

    public async Task<IActionResult> SearchResults(string cveId)
    {
        if (string.IsNullOrWhiteSpace(cveId))
        {
            return RedirectToAction("Index", new { searchMessage = "Please enter a CVE ID to search." });
        }

        try
        {
            // Search for ALL CVE records that contain this CVE ID in the Details column
            var cveRecords = await _context.CveUpdateStagings
                .Where(c => c.Details != null && c.Details.Contains(cveId))
                .OrderByDescending(c => c.ReleaseDate)
                .ToListAsync();

            if (!cveRecords.Any())
            {
                var notFoundMessage = $"No CVE was found with ID '{cveId}'. Please check the CVE ID and try again.";
                return RedirectToAction("Index", new { searchMessage = notFoundMessage });
            }

            ViewBag.SearchedCveId = cveId;
            ViewBag.CveDetails = ExtractCveDetails(cveRecords, cveId);
            
            return View(cveRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search results for CVE ID: {CveId}", cveId);
            var errorMessage = "An error occurred while retrieving search results. Please try again.";
            return RedirectToAction("Index", new { searchMessage = errorMessage });
        }
    }

    private dynamic ExtractCveDetails(List<CveUpdateStaging> records, string cveId)
    {
        // Extract common CVE information from the first record
        var firstRecord = records.First();
        
        return new
        {
            CveId = cveId,
            FirstDiscovered = records.Min(r => r.ReleaseDate),
            MaxSeverity = GetHighestSeverity(records),
            TotalAffectedProducts = records.Count,
            ProductFamilies = records.Select(r => r.ProductFamily).Distinct().Where(pf => !string.IsNullOrEmpty(pf)).ToList()
        };
    }

    private string GetHighestSeverity(List<CveUpdateStaging> records)
    {
        var severities = records.Select(r => r.MaxSeverity).Where(s => !string.IsNullOrEmpty(s)).ToList();
        
        // Define severity order (highest to lowest)
        var severityOrder = new[] { "Critical", "High", "Medium", "Low" };
        
        foreach (var severity in severityOrder)
        {
            if (severities.Any(s => s.Equals(severity, StringComparison.OrdinalIgnoreCase)))
            {
                return severity;
            }
        }
        
        return severities.FirstOrDefault() ?? "Unknown";
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
