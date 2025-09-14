using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using System.Globalization;

namespace CveWebApp.Controllers
{
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

        // GET: Cve/Import
        public IActionResult Import()
        {
            return View();
        }

        // POST: Cve/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a CSV file to upload.");
                return View();
            }

            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Please upload a CSV file.");
                return View();
            }

            try
            {
                var importedCount = 0;
                var updatedCount = 0;
                var errors = new List<string>();

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
                                // Check if record exists by Id
                                var existingRecord = await _context.CveUpdateStagings
                                    .FirstOrDefaultAsync(c => c.Id == cveRecord.Id);

                                if (existingRecord != null)
                                {
                                    // Update existing record
                                    UpdateExistingRecord(existingRecord, cveRecord);
                                    updatedCount++;
                                }
                                else
                                {
                                    // Add new record
                                    _context.CveUpdateStagings.Add(cveRecord);
                                    importedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Line {lineNumber}: {ex.Message}");
                        }
                    }
                }

                if (errors.Count > 0 && errors.Count < 10) // Only save if there are few errors
                {
                    await _context.SaveChangesAsync();
                    ViewBag.SuccessMessage = $"Import completed with {errors.Count} errors. Imported: {importedCount}, Updated: {updatedCount}";
                    ViewBag.Errors = errors;
                }
                else if (errors.Count == 0)
                {
                    await _context.SaveChangesAsync();
                    ViewBag.SuccessMessage = $"Import completed successfully! Imported: {importedCount}, Updated: {updatedCount}";
                }
                else
                {
                    ViewBag.ErrorMessage = $"Import failed with {errors.Count} errors. Please check your CSV file format.";
                    ViewBag.Errors = errors.Take(10).ToList(); // Show first 10 errors
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error processing file: {ex.Message}");
            }

            return View();
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
                            record.Id = int.Parse(value);
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
                            if (decimal.TryParse(value, out var baseScore))
                                record.BaseScore = baseScore;
                            break;
                        case "temporalscore":
                        case "temporal score":
                            if (decimal.TryParse(value, out var temporalScore))
                                record.TemporalScore = temporalScore;
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

        private void UpdateExistingRecord(CveUpdateStaging existing, CveUpdateStaging updated)
        {
            existing.ReleaseDate = updated.ReleaseDate ?? existing.ReleaseDate;
            existing.ProductFamily = updated.ProductFamily ?? existing.ProductFamily;
            existing.Product = updated.Product ?? existing.Product;
            existing.Platform = updated.Platform ?? existing.Platform;
            existing.Impact = updated.Impact ?? existing.Impact;
            existing.MaxSeverity = updated.MaxSeverity ?? existing.MaxSeverity;
            existing.Article = updated.Article ?? existing.Article;
            existing.ArticleLink = updated.ArticleLink ?? existing.ArticleLink;
            existing.Supercedence = updated.Supercedence ?? existing.Supercedence;
            existing.Download = updated.Download ?? existing.Download;
            existing.DownloadLink = updated.DownloadLink ?? existing.DownloadLink;
            existing.BuildNumber = updated.BuildNumber ?? existing.BuildNumber;
            existing.Details = updated.Details ?? existing.Details;
            existing.DetailsLink = updated.DetailsLink ?? existing.DetailsLink;
            existing.BaseScore = updated.BaseScore ?? existing.BaseScore;
            existing.TemporalScore = updated.TemporalScore ?? existing.TemporalScore;
            existing.CustomerActionRequired = updated.CustomerActionRequired ?? existing.CustomerActionRequired;
        }
    }
}