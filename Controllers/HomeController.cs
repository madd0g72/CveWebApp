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
            // Search for CVE in the Details column
            var cveRecord = await _context.CveUpdateStagings
                .FirstOrDefaultAsync(c => c.Details != null && c.Details.Contains(cleanCveId));

            if (cveRecord != null)
            {
                // Redirect to the details page for the found record
                return RedirectToAction("Details", "Cve", new { id = cveRecord.Id });
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
