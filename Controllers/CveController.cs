using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using System.Globalization;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for CVE data management - viewing is public, import is admin-only
    /// </summary>
    public class CveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 20; // Set your preferred page size here

        public CveController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Cve
        public async Task<IActionResult> Index(
            int page = 1,
            string productFamily = null,
            string product = null,
            string maxSeverity = null,
            string article = null)
        {
            var query = _context.CveUpdateStagings.AsQueryable();

            if (!string.IsNullOrEmpty(productFamily))
                query = query.Where(c => c.ProductFamily == productFamily);

            if (!string.IsNullOrEmpty(product))
                query = query.Where(c => c.Product == product);

            if (!string.IsNullOrEmpty(maxSeverity))
                query = query.Where(c => c.MaxSeverity == maxSeverity);

            if (!string.IsNullOrEmpty(article))
                query = query.Where(c => c.Article.Contains(article));

            var totalItems = await query.CountAsync();

            var cveData = await query
                .OrderByDescending(c => c.ReleaseDate)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // For filter dropdowns
            ViewBag.ProductFamilies = await _context.CveUpdateStagings
                .Select(c => c.ProductFamily).Distinct().OrderBy(x => x).ToListAsync();

            if (!string.IsNullOrEmpty(productFamily))
            {
                ViewBag.Products = await _context.CveUpdateStagings
                    .Where(c => c.ProductFamily == productFamily)
                    .Select(c => c.Product)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }
            else
            {
                ViewBag.Products = await _context.CveUpdateStagings
                    .Select(c => c.Product)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
            }

            ViewBag.MaxSeverities = await _context.CveUpdateStagings
                .Select(c => c.MaxSeverity).Distinct().OrderBy(x => x).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
            ViewBag.SelectedProductFamily = productFamily;
            ViewBag.SelectedProduct = product;
            ViewBag.SelectedMaxSeverity = maxSeverity;
            ViewBag.ArticleQuery = article;

            return View(cveData);
        }

        // GET: Cve/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cveUpdateStaging = await _context.CveUpdateStagings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (cveUpdateStaging == null)
            {
                return NotFound();
            }

            return View(cveUpdateStaging);
        }

        // GET: Cve/ComplianceOverview/5
        public async Task<IActionResult> ComplianceOverview(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cveDetails = await _context.CveUpdateStagings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (cveDetails == null)
            {
                return NotFound();
            }

            var viewModel = await BuildComplianceViewModelAsync(cveDetails);
            return View(viewModel);
        }

        private async Task<ComplianceViewModel> BuildComplianceViewModelAsync(CveUpdateStaging cveDetails)
        {
            var viewModel = new ComplianceViewModel
            {
                CveDetails = cveDetails
            };

            // Extract required KBs from the Article field
            viewModel.RequiredKbs = ExtractKbsFromArticle(cveDetails.Article);

            if (string.IsNullOrEmpty(cveDetails.Product) && string.IsNullOrEmpty(cveDetails.ProductFamily))
            {
                // No product information to match against
                return viewModel;
            }

            // Get all servers with matching OS products
            var matchingServers = await GetMatchingServersAsync(cveDetails.Product, cveDetails.ProductFamily);

            // Group servers by computer name and calculate compliance
            var serverGroups = matchingServers
                .GroupBy(s => new { s.Computer, s.OSProduct })
                .Select(g => new ServerComplianceStatus
                {
                    Computer = g.Key.Computer,
                    OSProduct = g.Key.OSProduct,
                    InstalledKbs = g.Select(s => s.KB).Distinct().ToList()
                })
                .ToList();

            // Calculate compliance for each server
            foreach (var server in serverGroups)
            {
                server.MissingKbs = viewModel.RequiredKbs
                    .Where(reqKb => !server.InstalledKbs.Any(installedKb => 
                        installedKb.Equals(reqKb, StringComparison.OrdinalIgnoreCase) ||
                        installedKb.Equals(reqKb.Replace("KB", ""), StringComparison.OrdinalIgnoreCase) ||
                        ("KB" + installedKb).Equals(reqKb, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                
                server.IsCompliant = viewModel.RequiredKbs.Count == 0 || server.MissingKbs.Count == 0;
            }

            viewModel.ServerStatuses = serverGroups.OrderBy(s => s.Computer).ToList();

            // Calculate summary
            viewModel.Summary = new ComplianceSummary
            {
                TotalServers = viewModel.ServerStatuses.Count,
                CompliantServers = viewModel.ServerStatuses.Count(s => s.IsCompliant),
                NonCompliantServers = viewModel.ServerStatuses.Count(s => !s.IsCompliant)
            };

            return viewModel;
        }

        private List<string> ExtractKbsFromArticle(string? article)
        {
            if (string.IsNullOrEmpty(article))
                return new List<string>();

            var kbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // First, look for KB patterns like "KB1234567" or "kb1234567"
            var kbPattern = new System.Text.RegularExpressions.Regex(@"\bKB\d{6,7}\b", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = kbPattern.Matches(article);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                kbs.Add(match.Value.ToUpper());
            }

            // Then, look for bare 6-7 digit numbers and normalize them to KB format
            var bareNumberPattern = new System.Text.RegularExpressions.Regex(@"\b\d{6,7}\b");
            var bareMatches = bareNumberPattern.Matches(article);
            foreach (System.Text.RegularExpressions.Match match in bareMatches)
            {
                var kbFormatted = "KB" + match.Value;
                kbs.Add(kbFormatted);
            }

            return kbs.ToList();
        }

        private async Task<List<ServerInstalledKb>> GetMatchingServersAsync(string? product, string? productFamily)
        {
            var query = _context.ServerInstalledKbs.AsQueryable();

            if (!string.IsNullOrEmpty(product))
            {
                query = query.Where(s => s.OSProduct.Contains(product));
            }
            else if (!string.IsNullOrEmpty(productFamily))
            {
                query = query.Where(s => s.OSProduct.Contains(productFamily));
            }

            return await query.ToListAsync();
        }

        // GET: Cve/Import - Admin only
        [Authorize(Roles = "Admin")]
        public IActionResult Import()
        {
            return View();
        }

        // POST: Cve/Import - Admin only
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Import(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a CSV file to upload.");
                return View();
            }

            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Please upload a CSV file with .csv extension.");
                return View();
            }

            // Check file size (limit to 10MB)
            if (csvFile.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError("", "File size cannot exceed 10MB.");
                return View();
            }

            try
            {
                var importedCount = 0;
                var errors = new List<string>();
                var warnings = new List<string>();

                using (var reader = new StringReader(await new StreamReader(csvFile.OpenReadStream()).ReadToEndAsync()))
                {
                    string? line;
                    var lineNumber = 0;
                    string[]? headers = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        
                        if (lineNumber == 1)
                        {
                            // Parse header row
                            headers = ParseCsvLine(line);
                            
                            // Validate that we have essential headers
                            if (!HasRequiredHeaders(headers))
                            {
                                ModelState.AddModelError("", "CSV file must contain at least one valid CVE data column. Please check the format requirements.");
                                return View();
                            }
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            var values = ParseCsvLine(line);
                            var cveRecord = ParseCveRecord(headers!, values, lineNumber);
                            
                            if (cveRecord != null)
                            {
                                // Since we no longer require Id in CSV, we'll identify duplicates by other fields
                                // For simplicity, we'll treat all records as new since Id is auto-generated
                                // In a production scenario, you might want to check for duplicates using other fields
                                _context.CveUpdateStagings.Add(cveRecord);
                                importedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Line {lineNumber}: {ex.Message}");
                        }
                    }
                }

                // Save changes if we have records to process and not too many errors
                if (importedCount > 0 && errors.Count < 10)
                {
                    await _context.SaveChangesAsync();
                    ViewBag.SuccessMessage = $"Import completed successfully! New records: {importedCount}";
                    
                    if (errors.Count > 0)
                    {
                        ViewBag.SuccessMessage += $" ({errors.Count} rows skipped due to errors)";
                        ViewBag.Warnings = errors;
                    }
                }
                else if (errors.Count >= 10)
                {
                    ViewBag.ErrorMessage = $"Import failed: Too many errors ({errors.Count}). Please check your CSV file format and fix the issues.";
                    ViewBag.Errors = errors.Take(10).ToList(); // Show first 10 errors
                }
                else if (importedCount == 0)
                {
                    ViewBag.ErrorMessage = "No valid records found to import. Please check your CSV file format.";
                    if (errors.Count > 0)
                    {
                        ViewBag.Errors = errors;
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error processing file: {ex.Message}");
            }

            return View();
        }

        private bool HasRequiredHeaders(string[] headers)
        {
            // No longer require 'Id' column - the database will auto-generate it
            // Just check that we have at least one valid header
            var validHeaders = new[] { "releasedate", "release date", "productfamily", "product family", 
                                     "product", "platform", "impact", "maxseverity", "max severity", 
                                     "article", "articlelink", "article link", "supercedence", "download", 
                                     "downloadlink", "download link", "buildnumber", "build number", 
                                     "details", "cve", "detailslink", "details link", "basescore", "base score", 
                                     "temporalscore", "temporal score", "customeractionrequired", "customer action required" };
            
            return headers.Any(h => validHeaders.Contains(h.Trim().ToLowerInvariant()));
        }

        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var inQuotes = false;
            var currentValue = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(currentValue.Trim());
                    currentValue = "";
                }
                else
                {
                    currentValue += c;
                }
            }

            values.Add(currentValue.Trim());
            return values.ToArray();
        }

        private CveUpdateStaging? ParseCveRecord(string[] headers, string[] values, int lineNumber)
        {
            if (headers.Length != values.Length)
            {
                throw new Exception($"Column count mismatch. Expected {headers.Length}, got {values.Length}");
            }

            var record = new CveUpdateStaging();

            for (int i = 0; i < headers.Length; i++)
            {
                var header = headers[i].Trim().ToLowerInvariant();
                var value = values[i].Trim();

                if (string.IsNullOrEmpty(value))
                    continue;

                try
                {
                    switch (header)
                    {
                        case "id":
                            // Skip Id field - let the database auto-generate it
                            break;
                        case "releasedate":
                        case "release date":
                            if (DateTime.TryParse(value, out var releaseDate))
                                record.ReleaseDate = releaseDate;
                            break;
                        case "productfamily":
                        case "product family":
                            record.ProductFamily = value;
                            break;
                        case "product":
                            record.Product = value;
                            break;
                        case "platform":
                            record.Platform = value;
                            break;
                        case "impact":
                            record.Impact = value;
                            break;
                        case "maxseverity":
                        case "max severity":
                            record.MaxSeverity = value;
                            break;
                        case "article":
                            record.Article = value;
                            break;
                        case "articlelink":
                        case "article link":
                            record.ArticleLink = value;
                            break;
                        case "supercedence":
                            record.Supercedence = value;
                            break;
                        case "download":
                            record.Download = value;
                            break;
                        case "downloadlink":
                        case "download link":
                            record.DownloadLink = value;
                            break;
                        case "buildnumber":
                        case "build number":
                            record.BuildNumber = value;
                            break;
                        case "details":
                        case "cve":
                            record.Details = value;
                            break;
                        case "detailslink":
                        case "details link":
                            record.DetailsLink = value;
                            break;
                        case "basescore":
                        case "base score":
                            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var baseScore))
                            {
                                if (baseScore >= 0.0m && baseScore <= 10.0m)
                                    record.BaseScore = baseScore;
                                // Note: Invalid range values are silently ignored to allow data import to continue
                            }
                            // Note: Invalid format values are silently ignored to allow data import to continue
                            break;
                        case "temporalscore":
                        case "temporal score":
                            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var temporalScore))
                            {
                                if (temporalScore >= 0.0m && temporalScore <= 10.0m)
                                    record.TemporalScore = temporalScore;
                                // Note: Invalid range values are silently ignored to allow data import to continue
                            }
                            // Note: Invalid format values are silently ignored to allow data import to continue
                            break;
                        case "customeractionrequired":
                        case "customer action required":
                            if (bool.TryParse(value, out var actionRequired))
                                record.CustomerActionRequired = actionRequired;
                            else if (value.ToLowerInvariant() == "yes" || value == "1")
                                record.CustomerActionRequired = true;
                            else if (value.ToLowerInvariant() == "no" || value == "0")
                                record.CustomerActionRequired = false;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error parsing column '{header}' with value '{value}': {ex.Message}");
                }
            }

            return record;
        }

    }
}