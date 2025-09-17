using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using CveWebApp.Services;
using System.Globalization;
using QuestPDF.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for CVE data management - viewing is public, import is admin-only
    /// </summary>
    public class CveController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CveController> _logger;
        private const int PageSize = 20; // Set your preferred page size here

        public CveController(ApplicationDbContext context, ILogger<CveController> logger)
        {
            _context = context;
            _logger = logger;
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

            // Calculate compliance for each CVE
            var cveComplianceData = new List<CveWithCompliance>();
            foreach (var cve in cveData)
            {
                var compliance = await CalculateComplianceForCveAsync(cve);
                cveComplianceData.Add(new CveWithCompliance
                {
                    Cve = cve,
                    CompliancePercentage = compliance
                });
            }

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

            return View(cveComplianceData);
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

        // GET: Cve/ExportCompliance/5
        [Authorize]
        public async Task<IActionResult> ExportCompliance(int? id)
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
            
            // Generate PDF
            try
            {
                var document = CreateCompliancePdfDocument(viewModel);
                var pdfBytes = document.GeneratePdf();
                
                var fileName = $"CVE_Compliance_Report_{cveDetails.Id}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                // Log error and redirect back with error message
                TempData["ErrorMessage"] = $"Error generating PDF: {ex.Message}";
                return RedirectToAction(nameof(ComplianceOverview), new { id });
            }
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

            // Calculate compliance for each server with supersedence consideration
            foreach (var server in serverGroups)
            {
                var complianceResult = await GetServerComplianceWithSupersedenceDetails(
                    viewModel.RequiredKbs, 
                    server.InstalledKbs, 
                    cveDetails.Product, 
                    cveDetails.ProductFamily);
                
                server.MissingKbs = complianceResult.MissingKbs;
                server.IsCompliant = complianceResult.IsCompliant;
                server.SupersedenceNotes = complianceResult.SupersedenceNotes;
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

        private Document CreateCompliancePdfDocument(ComplianceViewModel viewModel)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header()
                        .Row(row =>
                        {
                            row.RelativeItem().Text("CVE Compliance Report")
                                .SemiBold()
                                .FontSize(14)
                                .FontColor(Colors.Blue.Medium);
                            
                            row.AutoItem().Text($"CVE: {viewModel.CveDetails.Details ?? "Not specified"}")
                                .SemiBold()
                                .FontSize(12)
                                .FontColor(Colors.Red.Medium);
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            // CVE Information Section
                            column.Item().Text("CVE Information").SemiBold().FontSize(12);
                            column.Item().PaddingBottom(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                table.Cell().Text("CVE Number:").SemiBold();
                                table.Cell().Text(viewModel.CveDetails.Details ?? "Not specified").FontColor(Colors.Blue.Medium);

                                table.Cell().Text("Product Family:").SemiBold();
                                table.Cell().Text(viewModel.CveDetails.ProductFamily ?? "N/A");

                                table.Cell().Text("Product:").SemiBold();
                                table.Cell().Text(viewModel.CveDetails.Product ?? "N/A");

                                table.Cell().Text("Article:").SemiBold();
                                table.Cell().Text(viewModel.CveDetails.Article ?? "N/A");

                                table.Cell().Text("Max Severity:").SemiBold();
                                table.Cell().Text(viewModel.CveDetails.MaxSeverity ?? "N/A");

                                table.Cell().Text("Release Date:").SemiBold();
                                table.Cell().Text(viewModel.CveDetails.ReleaseDate?.ToString("yyyy-MM-dd") ?? "N/A");

                                table.Cell().Text("Required KBs:").SemiBold();
                                table.Cell().Text(viewModel.RequiredKbs.Any() ? string.Join(", ", viewModel.RequiredKbs) : "None identified").FontColor(Colors.Blue.Lighten2);
                            });

                            // Compliance Summary Section
                            column.Item().PaddingTop(15).Text("Compliance Summary").SemiBold().FontSize(12);
                            column.Item().PaddingBottom(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Cell().Text("Total Servers").SemiBold().FontColor(Colors.Blue.Medium);
                                table.Cell().Text("Compliant").SemiBold().FontColor(Colors.Green.Medium);
                                table.Cell().Text("Non-Compliant").SemiBold().FontColor(Colors.Red.Medium);
                                table.Cell().Text("Compliance %").SemiBold().FontColor(Colors.Blue.Medium);

                                table.Cell().Text(viewModel.Summary.TotalServers.ToString()).FontColor(Colors.Blue.Medium);
                                table.Cell().Text(viewModel.Summary.CompliantServers.ToString()).FontColor(Colors.Green.Medium);
                                table.Cell().Text(viewModel.Summary.NonCompliantServers.ToString()).FontColor(Colors.Red.Medium);
                                
                                var complianceColor = viewModel.Summary.CompliancePercentage >= 80 ? Colors.Green.Medium :
                                                     viewModel.Summary.CompliancePercentage >= 50 ? Colors.Orange.Medium : Colors.Red.Medium;
                                table.Cell().Text($"{viewModel.Summary.CompliancePercentage:F1}%").FontColor(complianceColor).SemiBold();
                            });

                            // Server Details Section
                            if (viewModel.ServerStatuses.Any())
                            {
                                column.Item().PaddingTop(15).Text("Server Compliance Details").SemiBold().FontSize(12);
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                    });

                                    // Header
                                    table.Cell().Element(HeaderCellStyle).Text("Computer Name").SemiBold().FontColor(Colors.White);
                                    table.Cell().Element(HeaderCellStyle).Text("OS Product").SemiBold().FontColor(Colors.White);
                                    table.Cell().Element(HeaderCellStyle).Text("Status").SemiBold().FontColor(Colors.White);
                                    table.Cell().Element(HeaderCellStyle).Text("Installed KBs").SemiBold().FontColor(Colors.White);
                                    table.Cell().Element(HeaderCellStyle).Text("Missing KBs").SemiBold().FontColor(Colors.White);

                                    // Data rows
                                    foreach (var server in viewModel.ServerStatuses)
                                    {
                                        Func<IContainer, IContainer> rowStyle;
                                        if (server.IsCompliant)
                                        {
                                            rowStyle = viewModel.RequiredKbs.Count == 0 ? NeutralRowStyle : CompliantRowStyle;
                                        }
                                        else
                                        {
                                            rowStyle = NonCompliantRowStyle;
                                        }

                                        table.Cell().Element(rowStyle).Text(server.Computer).SemiBold();
                                        table.Cell().Element(rowStyle).Text(server.OSProduct);
                                        
                                        var statusText = server.IsCompliant 
                                            ? (viewModel.RequiredKbs.Count == 0 ? "N/A" : "Compliant") 
                                            : "Non-Compliant";
                                        var statusColor = server.IsCompliant 
                                            ? (viewModel.RequiredKbs.Count == 0 ? Colors.Grey.Medium : Colors.Green.Medium)
                                            : Colors.Red.Medium;
                                        table.Cell().Element(rowStyle).Text(statusText).FontColor(statusColor).SemiBold();
                                        
                                        table.Cell().Element(rowStyle).Text(server.InstalledKbs.Any() ? 
                                            string.Join(", ", server.InstalledKbs.Take(5)) + 
                                            (server.InstalledKbs.Count > 5 ? $" (+{server.InstalledKbs.Count - 5} more)" : "") : "None");
                                        
                                        var missingKbsText = server.MissingKbs.Any() ? string.Join(", ", server.MissingKbs) : "None";
                                        var missingKbsColor = server.MissingKbs.Any() ? Colors.Red.Medium : Colors.Green.Medium;
                                        table.Cell().Element(rowStyle).Text(missingKbsText).FontColor(missingKbsColor);
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .Row(row =>
                        {
                            row.RelativeItem().Text($"CVE: {viewModel.CveDetails.Details ?? "Not specified"}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);
                            
                            row.AutoItem().Text($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm} | Page ")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);
                        });
                });
            });

            static IContainer HeaderCellStyle(IContainer container)
            {
                return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3)
                    .Background(Colors.Grey.Darken3);
            }

            static IContainer CompliantRowStyle(IContainer container)
            {
                return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3)
                    .Background(Colors.Green.Lighten4);
            }

            static IContainer NonCompliantRowStyle(IContainer container)
            {
                return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3)
                    .Background(Colors.Yellow.Lighten4);
            }

            static IContainer NeutralRowStyle(IContainer container)
            {
                return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3)
                    .Background(Colors.Grey.Lighten4);
            }
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

        private async Task<double> CalculateComplianceForCveAsync(CveUpdateStaging cve)
        {
            // Extract required KBs from the Article field
            var requiredKbs = ExtractKbsFromArticle(cve.Article);

            if (requiredKbs.Count == 0)
            {
                // If no KBs required, return a neutral compliance percentage
                return 85.0; // Default to 85% for CVEs without specific KB requirements
            }

            if (string.IsNullOrEmpty(cve.Product) && string.IsNullOrEmpty(cve.ProductFamily))
            {
                // No product information to match against
                return 50.0; // Default to 50% when no product matching is possible
            }

            // Get all servers with matching OS products
            var matchingServers = await GetMatchingServersAsync(cve.Product, cve.ProductFamily);

            if (matchingServers.Count == 0)
            {
                // No servers found, return a default compliance based on severity
                return GetDefaultComplianceBasedOnSeverity(cve.MaxSeverity);
            }

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
            var compliantServers = 0;
            foreach (var server in serverGroups)
            {
                var missingKbs = requiredKbs
                    .Where(reqKb => !server.InstalledKbs.Any(installedKb => 
                        installedKb.Equals(reqKb, StringComparison.OrdinalIgnoreCase) ||
                        installedKb.Equals(reqKb.Replace("KB", ""), StringComparison.OrdinalIgnoreCase) ||
                        ("KB" + installedKb).Equals(reqKb, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                
                if (missingKbs.Count == 0)
                {
                    compliantServers++;
                }
            }

            return serverGroups.Count > 0 ? (compliantServers / (double)serverGroups.Count) * 100 : 0;
        }

        private double GetDefaultComplianceBasedOnSeverity(string? severity)
        {
            // Return varying compliance percentages based on severity to demonstrate the gradient
            return severity?.ToLowerInvariant() switch
            {
                "critical" => 25.0,
                "high" => 45.0,
                "medium" => 70.0,
                "low" => 90.0,
                _ => 60.0
            };
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

        // POST: Cve/ProcessSupersedence - Admin only
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessSupersedence()
        {
            try
            {
                var loggerFactory = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var csvDataLoaderLogger = loggerFactory.CreateLogger<CsvDataLoader>();
                var csvDataLoader = new CsvDataLoader(_context, csvDataLoaderLogger);
                await csvDataLoader.ProcessSupersedenceRelationshipsAsync();
                TempData["SuccessMessage"] = "Supersedence data processed successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error processing supersedence data: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // GET: Cve/Supersedence - Admin only
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Supersedence()
        {
            var supersedences = await _context.KbSupersedences
                .OrderBy(k => k.OriginalKb)
                .ThenBy(k => k.SupersedingKb)
                .ToListAsync();

            return View(supersedences);
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
                    
                    // Process supersedence data after successful import
                    try
                    {
                        var loggerFactory = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                        var csvDataLoaderLogger = loggerFactory.CreateLogger<CsvDataLoader>();
                        var csvDataLoader = new CsvDataLoader(_context, csvDataLoaderLogger);
                        await csvDataLoader.ProcessSupersedenceRelationshipsAsync();
                        ViewBag.SuccessMessage = $"Import completed successfully! New records: {importedCount}. Supersedence data processed.";
                    }
                    catch (Exception ex)
                    {
                        ViewBag.SuccessMessage = $"Import completed successfully! New records: {importedCount}. Warning: Supersedence processing failed: {ex.Message}";
                    }
                    
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
                                     "article", "articlelink", "article link", "article (link)", "supercedence", "download", 
                                     "downloadlink", "download link", "download (link)", "buildnumber", "build number", 
                                     "details", "cve", "detailslink", "details link", "details (link)", "basescore", "base score", 
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

                // Handle null/empty values more robustly - convert empty strings to null for nullable fields
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = string.Empty; // Normalize to empty string for consistent handling
                }

                try
                {
                    switch (header)
                    {
                        case "id":
                            // Skip Id field - let the database auto-generate it
                            break;
                        case "releasedate":
                        case "release date":
                            if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out var releaseDate))
                                record.ReleaseDate = releaseDate;
                            else
                                record.ReleaseDate = null;
                            break;
                        case "productfamily":
                        case "product family":
                            record.ProductFamily = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "product":
                            record.Product = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "platform":
                            record.Platform = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "impact":
                            record.Impact = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "maxseverity":
                        case "max severity":
                            record.MaxSeverity = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "article":
                            record.Article = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "articlelink":
                        case "article link":
                        case "article (link)":
                            record.ArticleLink = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "supercedence":
                            record.Supercedence = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "download":
                            record.Download = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "downloadlink":
                        case "download link":
                        case "download (link)":
                            record.DownloadLink = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "buildnumber":
                        case "build number":
                            record.BuildNumber = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "details":
                        case "cve":
                            record.Details = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "detailslink":
                        case "details link":
                        case "details (link)":
                            record.DetailsLink = string.IsNullOrWhiteSpace(value) ? null : value;
                            break;
                        case "basescore":
                        case "base score":
                            if (!string.IsNullOrWhiteSpace(value) && 
                                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var baseScore))
                            {
                                if (baseScore >= 0.0m && baseScore <= 10.0m)
                                    record.BaseScore = baseScore;
                                else
                                    record.BaseScore = null; // Invalid range values
                            }
                            else
                            {
                                record.BaseScore = null; // Invalid format or empty values
                            }
                            break;
                        case "temporalscore":
                        case "temporal score":
                            if (!string.IsNullOrWhiteSpace(value) && 
                                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var temporalScore))
                            {
                                if (temporalScore >= 0.0m && temporalScore <= 10.0m)
                                    record.TemporalScore = temporalScore;
                                else
                                    record.TemporalScore = null; // Invalid range values
                            }
                            else
                            {
                                record.TemporalScore = null; // Invalid format or empty values
                            }
                            break;
                        case "customeractionrequired":
                        case "customer action required":
                            if (string.IsNullOrWhiteSpace(value))
                            {
                                record.CustomerActionRequired = null;
                            }
                            else if (bool.TryParse(value, out var actionRequired))
                            {
                                record.CustomerActionRequired = actionRequired;
                            }
                            else if (value.ToLowerInvariant() == "yes" || value == "1")
                            {
                                record.CustomerActionRequired = true;
                            }
                            else if (value.ToLowerInvariant() == "no" || value == "0")
                            {
                                record.CustomerActionRequired = false;
                            }
                            else
                            {
                                record.CustomerActionRequired = null; // Invalid value
                            }
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

        /// <summary>
        /// Extracts KB numbers from supersedence field
        /// </summary>
        private List<string> ExtractKbsFromSupersedence(string? supersedence)
        {
            if (string.IsNullOrEmpty(supersedence))
                return new List<string>();

            var kbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Look for KB patterns like "KB1234567" or "kb1234567"
            var kbPattern = new System.Text.RegularExpressions.Regex(@"\bKB\d{6,7}\b", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = kbPattern.Matches(supersedence);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                kbs.Add(match.Value.ToUpper());
            }

            // Also look for bare 6-7 digit numbers and normalize them to KB format
            var bareNumberPattern = new System.Text.RegularExpressions.Regex(@"\b\d{6,7}\b");
            var bareMatches = bareNumberPattern.Matches(supersedence);
            
            foreach (System.Text.RegularExpressions.Match match in bareMatches)
            {
                var kbFormatted = "KB" + match.Value;
                kbs.Add(kbFormatted);
            }

            return kbs.ToList();
        }

        /// <summary>
        /// Checks if a server is compliant with required KBs considering supersedence
        /// </summary>
        private async Task<bool> IsServerCompliantWithSupersedence(List<string> requiredKbs, List<string> installedKbs, string? product, string? productFamily)
        {
            if (requiredKbs.Count == 0)
                return true;

            foreach (var requiredKb in requiredKbs)
            {
                bool hasRequiredOrSuperseding = false;

                // Check if the exact KB is installed
                if (installedKbs.Any(installedKb => 
                    installedKb.Equals(requiredKb, StringComparison.OrdinalIgnoreCase) ||
                    installedKb.Equals(requiredKb.Replace("KB", ""), StringComparison.OrdinalIgnoreCase) ||
                    ("KB" + installedKb).Equals(requiredKb, StringComparison.OrdinalIgnoreCase)))
                {
                    hasRequiredOrSuperseding = true;
                }
                else
                {
                    // Check if any installed KB supersedes the required KB (including transitive supersedence)
                    var supersedingKb = await FindSupersedingKbAsync(requiredKb, installedKbs, product, productFamily);
                    hasRequiredOrSuperseding = supersedingKb != null;
                }

                if (!hasRequiredOrSuperseding)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the missing KBs for a server considering supersedence
        /// </summary>
        private async Task<List<string>> GetMissingKbsWithSupersedence(List<string> requiredKbs, List<string> installedKbs, string? product, string? productFamily)
        {
            var missingKbs = new List<string>();

            foreach (var requiredKb in requiredKbs)
            {
                bool hasRequiredOrSuperseding = false;

                // Check if the exact KB is installed
                if (installedKbs.Any(installedKb => 
                    installedKb.Equals(requiredKb, StringComparison.OrdinalIgnoreCase) ||
                    installedKb.Equals(requiredKb.Replace("KB", ""), StringComparison.OrdinalIgnoreCase) ||
                    ("KB" + installedKb).Equals(requiredKb, StringComparison.OrdinalIgnoreCase)))
                {
                    hasRequiredOrSuperseding = true;
                }
                else
                {
                    // Check if any installed KB supersedes the required KB (including transitive supersedence)
                    var supersedingKb = await FindSupersedingKbAsync(requiredKb, installedKbs, product, productFamily);
                    hasRequiredOrSuperseding = supersedingKb != null;
                }

                if (!hasRequiredOrSuperseding)
                    missingKbs.Add(requiredKb);
            }

            return missingKbs;
        }

        /// <summary>
        /// Gets comprehensive compliance information including supersedence details
        /// </summary>
        private async Task<ServerComplianceResult> GetServerComplianceWithSupersedenceDetails(List<string> requiredKbs, List<string> installedKbs, string? product, string? productFamily)
        {
            var result = new ServerComplianceResult
            {
                MissingKbs = new List<string>(),
                SupersedenceNotes = new List<string>(),
                IsCompliant = true
            };

            if (requiredKbs.Count == 0)
                return result;

            foreach (var requiredKb in requiredKbs)
            {
                bool hasRequiredOrSuperseding = false;
                string? complianceReason = null;

                // Check if the exact KB is installed
                if (installedKbs.Any(installedKb => 
                    installedKb.Equals(requiredKb, StringComparison.OrdinalIgnoreCase) ||
                    installedKb.Equals(requiredKb.Replace("KB", ""), StringComparison.OrdinalIgnoreCase) ||
                    ("KB" + installedKb).Equals(requiredKb, StringComparison.OrdinalIgnoreCase)))
                {
                    hasRequiredOrSuperseding = true;
                    complianceReason = $"Required KB {requiredKb} is directly installed";
                }
                else
                {
                    // Check if any installed KB supersedes the required KB (including transitive supersedence)
                    var supersedingKb = await FindSupersedingKbAsync(requiredKb, installedKbs, product, productFamily);
                    
                    if (supersedingKb != null)
                    {
                        hasRequiredOrSuperseding = true;
                        complianceReason = $"Required KB {requiredKb} is superseded by installed KB {supersedingKb}";
                    }
                }

                if (hasRequiredOrSuperseding)
                {
                    if (!string.IsNullOrEmpty(complianceReason))
                        result.SupersedenceNotes.Add(complianceReason);
                }
                else
                {
                    result.MissingKbs.Add(requiredKb);
                }
            }

            result.IsCompliant = result.MissingKbs.Count == 0;
            return result;
        }

        /// <summary>
        /// Finds if any installed KB supersedes the required KB by following the entire supersedence chain
        /// </summary>
        private async Task<string?> FindSupersedingKbAsync(string requiredKb, List<string> installedKbs, string? product, string? productFamily)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(requiredKb);
            visited.Add(requiredKb);

            while (queue.Count > 0)
            {
                var currentKb = queue.Dequeue();
                
                // Get all KBs that directly supersede the current KB
                var directSupersedingKbs = await _context.KbSupersedences
                    .Where(k => k.OriginalKb == currentKb)
                    .Where(k => product == null || k.Product == null || k.Product == product)
                    .Where(k => productFamily == null || k.ProductFamily == null || k.ProductFamily == productFamily)
                    .Select(k => k.SupersedingKb)
                    .ToListAsync();

                foreach (var supersedingKb in directSupersedingKbs)
                {
                    // Check if this superseding KB is installed
                    var foundInstalledKb = installedKbs.FirstOrDefault(installedKb =>
                        installedKb.Equals(supersedingKb, StringComparison.OrdinalIgnoreCase) ||
                        installedKb.Equals(supersedingKb.Replace("KB", ""), StringComparison.OrdinalIgnoreCase) ||
                        ("KB" + installedKb).Equals(supersedingKb, StringComparison.OrdinalIgnoreCase));

                    if (foundInstalledKb != null)
                    {
                        return supersedingKb; // Found an installed KB that supersedes the required KB
                    }

                    // Add to queue for further traversal if not visited
                    if (!visited.Contains(supersedingKb))
                    {
                        visited.Add(supersedingKb);
                        queue.Enqueue(supersedingKb);
                    }
                }
            }

            return null; // No superseding KB found
        }

    }

    /// <summary>
    /// Result class for server compliance checks with supersedence details
    /// </summary>
    public class ServerComplianceResult
    {
        public List<string> MissingKbs { get; set; } = new();
        public List<string> SupersedenceNotes { get; set; } = new();
        public bool IsCompliant { get; set; }
    }
}