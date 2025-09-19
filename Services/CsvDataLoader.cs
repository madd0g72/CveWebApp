using System.Globalization;
using CveWebApp.Data;
using CveWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CveWebApp.Services
{
    /// <summary>
    /// Service for loading real CSV data into the database for development/testing
    /// </summary>
    public class CsvDataLoader
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CsvDataLoader> _logger;

        public CsvDataLoader(ApplicationDbContext context, ILogger<CsvDataLoader> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Loads MSRC security updates CSV data into the Staging table
        /// </summary>
        public async Task LoadMsrcSecurityUpdatesAsync(string csvFilePath)
        {
            if (!File.Exists(csvFilePath))
            {
                _logger.LogWarning("MSRC CSV file not found: {FilePath}", csvFilePath);
                return;
            }

            _logger.LogInformation("Loading MSRC security updates from: {FilePath}", csvFilePath);

            var records = new List<CveUpdateStaging>();
            var lines = await File.ReadAllLinesAsync(csvFilePath);
            
            if (lines.Length < 2)
            {
                _logger.LogWarning("MSRC CSV file has no data rows");
                return;
            }

            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length >= 17) // Ensure we have all required columns
                    {
                        var record = new CveUpdateStaging
                        {
                            ReleaseDate = ParseDate(values[0]),
                            ProductFamily = values[1],
                            Product = values[2],
                            Platform = values[3],
                            Impact = values[4],
                            MaxSeverity = values[5],
                            Article = values[6],
                            ArticleLink = values[7],
                            Supercedence = values[8],
                            Download = values[9],
                            DownloadLink = values[10],
                            BuildNumber = values[11],
                            Details = values[12], // This contains the CVE identifier
                            DetailsLink = values[13],
                            BaseScore = ParseDecimal(values[14]),
                            TemporalScore = ParseDecimal(values[15]),
                            CustomerActionRequired = ParseBoolean(values[16])
                        };

                        records.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing MSRC CSV line {LineNumber}: {Error}", i + 1, ex.Message);
                }
            }

            if (records.Any())
            {
                _context.CveUpdateStagings.AddRange(records);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Loaded {Count} MSRC security update records", records.Count);
            }
        }

        /// <summary>
        /// Loads WSUS installed KBs CSV data into the ServerInstalledKbs table
        /// </summary>
        public async Task LoadWsusInstalledKbsAsync(string csvFilePath)
        {
            if (!File.Exists(csvFilePath))
            {
                _logger.LogWarning("WSUS CSV file not found: {FilePath}", csvFilePath);
                return;
            }

            _logger.LogInformation("Loading WSUS installed KBs from: {FilePath}", csvFilePath);

            var records = new List<ServerInstalledKb>();
            var lines = await File.ReadAllLinesAsync(csvFilePath);
            
            if (lines.Length < 2)
            {
                _logger.LogWarning("WSUS CSV file has no data rows");
                return;
            }

            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length >= 3)
                    {
                        var computer = values[0].Trim('"');
                        var osProduct = values[1].Trim('"');
                        var installedKbs = values[2].Trim('"');

                        // Split the comma-separated KB list
                        var kbArray = installedKbs.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        
                        foreach (var kb in kbArray)
                        {
                            var kbNumber = kb.Trim();
                            if (!string.IsNullOrEmpty(kbNumber))
                            {
                                records.Add(new ServerInstalledKb
                                {
                                    Computer = computer,
                                    OSProduct = osProduct,
                                    KB = kbNumber
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing WSUS CSV line {LineNumber}: {Error}", i + 1, ex.Message);
                }
            }

            if (records.Any())
            {
                _context.ServerInstalledKbs.AddRange(records);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Loaded {Count} installed KB records for {ServerCount} servers", 
                    records.Count, records.Select(r => r.Computer).Distinct().Count());
            }
        }

        /// <summary>
        /// Processes supersedence relationships from the loaded CVE data
        /// </summary>
        public async Task ProcessSupersedenceRelationshipsAsync()
        {
            _logger.LogInformation("Processing supersedence relationships from CVE data");

            // Step 1: Clear existing supersedence data to rebuild fresh chains
            var existingCount = await _context.KbSupersedences.CountAsync();
            if (existingCount > 0)
            {
                _logger.LogInformation("Clearing {Count} existing supersedence relationships for fresh rebuild", existingCount);
                _context.KbSupersedences.RemoveRange(_context.KbSupersedences);
                await _context.SaveChangesAsync();
            }

            // Step 2: Build direct supersedence relationships from CVE data
            await BuildDirectSupersedenceRelationshipsAsync();

            // Step 3: Build transitive supersedence chains to ensure comprehensive coverage
            await BuildTransitiveSupersedenceRelationshipsAsync();

            _logger.LogInformation("Supersedence relationship processing completed");
        }

        /// <summary>
        /// Builds direct supersedence relationships from CVE data
        /// </summary>
        private async Task BuildDirectSupersedenceRelationshipsAsync()
        {
            _logger.LogInformation("Building direct supersedence relationships from CVE data");

            var cveRecords = await _context.CveUpdateStagings
                .Where(c => !string.IsNullOrEmpty(c.Supercedence) && !string.IsNullOrEmpty(c.Article))
                .ToListAsync();

            var supersedenceRecords = new List<KbSupersedence>();

            foreach (var cveRecord in cveRecords)
            {
                try
                {
                    var requiredKbs = ExtractKbsFromArticle(cveRecord.Article);
                    var supersededKbs = ExtractKbsFromSupersedence(cveRecord.Supercedence);

                    foreach (var requiredKb in requiredKbs)
                    {
                        foreach (var supersededKb in supersededKbs)
                        {
                            // Check if this supersedence relationship already exists
                            var existingSupersedence = supersedenceRecords
                                .FirstOrDefault(k => k.OriginalKb == supersededKb && k.SupersedingKb == requiredKb);

                            if (existingSupersedence == null)
                            {
                                supersedenceRecords.Add(new KbSupersedence
                                {
                                    OriginalKb = supersededKb,
                                    SupersedingKb = requiredKb,
                                    Product = cveRecord.Product,
                                    ProductFamily = cveRecord.ProductFamily,
                                    DateAdded = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error processing supersedence for CVE record {Id}: {Error}", cveRecord.Id, ex.Message);
                }
            }

            if (supersedenceRecords.Any())
            {
                _context.KbSupersedences.AddRange(supersedenceRecords);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Built {Count} direct supersedence relationships", supersedenceRecords.Count);
            }
        }

        /// <summary>
        /// Builds transitive supersedence relationships to create comprehensive chains
        /// If A supersedes B and B supersedes C, this creates A supersedes C relationships
        /// </summary>
        private async Task BuildTransitiveSupersedenceRelationshipsAsync()
        {
            _logger.LogInformation("Building transitive supersedence relationships");

            var maxIterations = 10; // Prevent infinite loops in circular dependencies
            var iterationCount = 0;
            var newRelationshipsAdded = true;

            while (newRelationshipsAdded && iterationCount < maxIterations)
            {
                iterationCount++;
                _logger.LogInformation("Transitive supersedence iteration {Iteration}", iterationCount);

                newRelationshipsAdded = false;

                // Get all current supersedence relationships
                var currentRelationships = await _context.KbSupersedences
                    .Select(k => new { k.OriginalKb, k.SupersedingKb, k.Product, k.ProductFamily })
                    .ToListAsync();

                var newTransitiveRelationships = new List<KbSupersedence>();

                // For each relationship A → B, find all relationships B → C to create A → C
                foreach (var relationship in currentRelationships)
                {
                    var transitiveTargets = currentRelationships
                        .Where(r => r.OriginalKb == relationship.SupersedingKb)
                        .Where(r => r.SupersedingKb != relationship.OriginalKb) // Avoid circular references
                        .ToList();

                    foreach (var transitiveTarget in transitiveTargets)
                    {
                        // Check if A → C relationship already exists
                        var existsInDb = await _context.KbSupersedences
                            .AnyAsync(k => k.OriginalKb == relationship.OriginalKb && 
                                          k.SupersedingKb == transitiveTarget.SupersedingKb);

                        var existsInNewList = newTransitiveRelationships
                            .Any(k => k.OriginalKb == relationship.OriginalKb && 
                                     k.SupersedingKb == transitiveTarget.SupersedingKb);

                        if (!existsInDb && !existsInNewList)
                        {
                            newTransitiveRelationships.Add(new KbSupersedence
                            {
                                OriginalKb = relationship.OriginalKb,
                                SupersedingKb = transitiveTarget.SupersedingKb,
                                Product = relationship.Product ?? transitiveTarget.Product,
                                ProductFamily = relationship.ProductFamily ?? transitiveTarget.ProductFamily,
                                DateAdded = DateTime.UtcNow
                            });
                            newRelationshipsAdded = true;
                        }
                    }
                }

                if (newTransitiveRelationships.Any())
                {
                    _context.KbSupersedences.AddRange(newTransitiveRelationships);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Added {Count} transitive supersedence relationships in iteration {Iteration}", 
                        newTransitiveRelationships.Count, iterationCount);
                }
            }

            if (iterationCount >= maxIterations)
            {
                _logger.LogWarning("Reached maximum iterations ({MaxIterations}) for transitive supersedence building", maxIterations);
            }

            var totalRelationships = await _context.KbSupersedences.CountAsync();
            _logger.LogInformation("Transitive supersedence building completed. Total relationships: {Total}", totalRelationships);
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

        private DateTime? ParseDate(string dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return null;

            // Try various date formats that might be in the CSV
            string[] formats = { "d MMM yyyy", "dd MMM yyyy", "M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" };
            
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                    return result;
            }

            if (DateTime.TryParse(dateStr, out DateTime parseResult))
                return parseResult;

            return null;
        }

        private decimal? ParseDecimal(string decimalStr)
        {
            if (string.IsNullOrEmpty(decimalStr))
                return null;

            if (decimal.TryParse(decimalStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;

            return null;
        }

        private bool? ParseBoolean(string boolStr)
        {
            if (string.IsNullOrEmpty(boolStr))
                return null;

            if (bool.TryParse(boolStr, out bool result))
                return result;

            // Handle common variations
            var lower = boolStr.ToLowerInvariant();
            if (lower == "yes" || lower == "1")
                return true;
            if (lower == "no" || lower == "0")
                return false;

            return null;
        }

        private List<string> ExtractKbsFromArticle(string? article)
        {
            var kbs = new List<string>();
            if (string.IsNullOrEmpty(article))
                return kbs;

            // Extract KB numbers from article field
            var parts = article.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var cleanPart = part.Trim();
                if (cleanPart.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                    (cleanPart.All(char.IsDigit) && cleanPart.Length >= 6))
                {
                    if (!cleanPart.StartsWith("KB", StringComparison.OrdinalIgnoreCase))
                        cleanPart = "KB" + cleanPart;
                    
                    kbs.Add(cleanPart);
                }
            }

            return kbs;
        }

        private List<string> ExtractKbsFromSupersedence(string? supersedence)
        {
            var kbs = new List<string>();
            if (string.IsNullOrEmpty(supersedence))
                return kbs;

            // Extract KB numbers from supersedence field
            var parts = supersedence.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var cleanPart = part.Trim();
                if (cleanPart.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                    (cleanPart.All(char.IsDigit) && cleanPart.Length >= 6))
                {
                    if (!cleanPart.StartsWith("KB", StringComparison.OrdinalIgnoreCase))
                        cleanPart = "KB" + cleanPart;
                    
                    kbs.Add(cleanPart);
                }
            }

            return kbs;
        }
    }
}