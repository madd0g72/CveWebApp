using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using System.Globalization;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for KB import functionality - restricted to Admin role only
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class KbImportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KbImportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: KbImport
        public IActionResult Index()
        {
            return View();
        }

        // POST: KbImport/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                ModelState.AddModelError("", "Please select a CSV file to upload.");
                return View("Index");
            }

            if (!Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "Please upload a CSV file.");
                return View("Index");
            }

            try
            {
                var importResult = await ProcessCsvFileAsync(csvFile);
                importResult.FileName = csvFile.FileName;
                importResult.ExitCode = importResult.IsSuccessful ? 0 : 1;
                
                // Pass results via ViewBag to stay on Index view
                ViewBag.ImportResult = importResult;
                ViewBag.SuccessMessage = importResult.IsSuccessful ? "The KB data has been imported successfully." : null;
                ViewBag.ErrorMessage = importResult.HasErrors ? "Some issues were encountered during the import process." : null;
                ViewBag.Errors = importResult.HasErrors ? importResult.Errors : null;
                
                // Import summary data for footer
                ViewBag.FileName = importResult.FileName;
                ViewBag.ExitCode = importResult.ExitCode;
                ViewBag.ImportStartTime = importResult.ImportStartTime;
                ViewBag.ImportEndTime = importResult.ImportEndTime;
                ViewBag.ProcessingDuration = importResult.ProcessingDuration?.TotalSeconds;
                ViewBag.LinesProcessed = importResult.LinesProcessed;
                ViewBag.ServersUpdated = importResult.ServersUpdated;
                ViewBag.RecordsAdded = importResult.RecordsAdded;
                ViewBag.RecordsRemoved = importResult.RecordsRemoved;
                ViewBag.ErrorCount = importResult.ErrorCount;
                
                return View("Index");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Error processing file: {ex.Message}";
                return View("Index");
            }
        }

        private async Task<KbImportResult> ProcessCsvFileAsync(IFormFile csvFile)
        {
            var result = new KbImportResult
            {
                ImportStartTime = DateTime.UtcNow
            };
            var serversToUpdate = new HashSet<string>();
            var newKbRecords = new List<ServerInstalledKb>();

            using var reader = new StreamReader(csvFile.OpenReadStream());
            
            // Read and validate header
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                throw new InvalidOperationException("CSV file is empty.");

            var headers = headerLine.Split(',').Select(h => NormalizeHeaderName(h)).ToArray();
            if (headers.Length < 3 || !headers[0].Equals("Computer", StringComparison.OrdinalIgnoreCase) ||
                !headers[1].Equals("OSProduct", StringComparison.OrdinalIgnoreCase) ||
                !headers[2].Equals("InstalledKBs", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CSV file must have headers: Computer,OSProduct,InstalledKBs");
            }

            int lineNumber = 1;
            string? line;
            
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var columns = ParseCsvLine(line);
                    if (columns.Length < 3)
                    {
                        result.Errors.Add($"Line {lineNumber}: Insufficient columns");
                        continue;
                    }

                    var computer = columns[0].Trim();
                    var osProduct = columns[1].Trim();
                    var installedKbs = columns[2].Trim();

                    if (string.IsNullOrWhiteSpace(computer) || string.IsNullOrWhiteSpace(osProduct))
                    {
                        result.Errors.Add($"Line {lineNumber}: Computer and OSProduct cannot be empty");
                        continue;
                    }

                    serversToUpdate.Add(computer);

                    // Parse comma-separated KB numbers
                    if (!string.IsNullOrWhiteSpace(installedKbs))
                    {
                        var kbNumbers = installedKbs.Split(',')
                            .Select(kb => kb.Trim())
                            .Where(kb => !string.IsNullOrWhiteSpace(kb));

                        foreach (var kb in kbNumbers)
                        {
                            // Validate KB format (should be numeric)
                            if (!kb.All(char.IsDigit))
                            {
                                result.Errors.Add($"Line {lineNumber}: Invalid KB format '{kb}' (should be numeric)");
                                continue;
                            }

                            newKbRecords.Add(new ServerInstalledKb
                            {
                                Computer = computer,
                                OSProduct = osProduct,
                                KB = kb                                
                            });
                        }
                    }

                    result.LinesProcessed++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                }
            }

            // Remove existing records for servers being updated
            if (serversToUpdate.Count > 0)
            {
                var existingRecords = await _context.ServerInstalledKbs
                    .Where(kb => serversToUpdate.Contains(kb.Computer))
                    .ToListAsync();
                
                result.RecordsRemoved = existingRecords.Count;
                _context.ServerInstalledKbs.RemoveRange(existingRecords);
            }

            // Add new records
            if (newKbRecords.Count > 0)
            {
                await _context.ServerInstalledKbs.AddRangeAsync(newKbRecords);
            }

            result.RecordsAdded = newKbRecords.Count;
            result.ServersUpdated = serversToUpdate.Count;

            // Save changes
            await _context.SaveChangesAsync();
            
            result.ImportEndTime = DateTime.UtcNow;

            return result;
        }

        private static string[] ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        current.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of column
                    columns.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add last column
            columns.Add(current.ToString());

            return columns.ToArray();
        }

        /// <summary>
        /// Normalizes header names by removing quotes and trimming whitespace
        /// </summary>
        /// <param name="header">The raw header value</param>
        /// <returns>Normalized header name</returns>
        private static string NormalizeHeaderName(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return string.Empty;

            // Trim whitespace first
            var trimmed = header.Trim();
            
            // Remove surrounding quotes if present
            if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2);
            }
            
            // Trim again in case there was whitespace inside quotes
            return trimmed.Trim();
        }
    }

    public class KbImportResult
    {
        public int LinesProcessed { get; set; }
        public int RecordsAdded { get; set; }
        public int RecordsRemoved { get; set; }
        public int ServersUpdated { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public bool HasErrors => Errors.Count > 0;
        public bool IsSuccessful => LinesProcessed > 0 && !HasErrors;
        
        // Progress tracking fields
        public string? FileName { get; set; }
        public int ExitCode { get; set; }
        public DateTime? ImportStartTime { get; set; }
        public DateTime? ImportEndTime { get; set; }
        public int ErrorCount => Errors.Count;
        
        public TimeSpan? ProcessingDuration => ImportStartTime.HasValue && ImportEndTime.HasValue 
            ? ImportEndTime.Value - ImportStartTime.Value 
            : null;
    }
}