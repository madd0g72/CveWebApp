using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using System.Globalization;

namespace CveWebApp.Controllers
{
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
                return View("ImportResult", importResult);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error processing file: {ex.Message}");
                return View("Index");
            }
        }

        private async Task<KbImportResult> ProcessCsvFileAsync(IFormFile csvFile)
        {
            var result = new KbImportResult();
            var serversToUpdate = new HashSet<string>();
            var newKbRecords = new List<ServerInstalledKb>();

            using var reader = new StreamReader(csvFile.OpenReadStream());
            
            // Read and validate header
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                throw new InvalidOperationException("CSV file is empty.");

            var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();
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
    }
}