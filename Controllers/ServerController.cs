using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using CveWebApp.Services;
using System.Globalization;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for server asset management - viewing is public, import/edit is admin-only
    /// </summary>
    public class ServerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServerController> _logger;
        private readonly IFileLoggingService _fileLoggingService;
        private const int PageSize = 20;

        public ServerController(ApplicationDbContext context, ILogger<ServerController> logger, IFileLoggingService fileLoggingService)
        {
            _context = context;
            _logger = logger;
            _fileLoggingService = fileLoggingService;
        }

        // GET: Server
        public async Task<IActionResult> Index(
            int page = 1,
            string? environmentFilter = null,
            string? projectFilter = null,
            string? operatingSystemFilter = null,
            string? serverNameFilter = null)
        {
            // Log server dashboard access
            var currentUser = User.Identity?.Name ?? "Anonymous";
            var sourceIP = GetClientIpAddress();
            
            await _fileLoggingService.LogActionAsync(
                "Server Dashboard Access", 
                currentUser, 
                $"Accessed Server Dashboard (page {page})", 
                sourceIP);

            var query = _context.Servers.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(environmentFilter))
            {
                query = query.Where(s => s.Environment != null && s.Environment.Contains(environmentFilter));
            }

            if (!string.IsNullOrEmpty(projectFilter))
            {
                query = query.Where(s => s.Project != null && s.Project.Contains(projectFilter));
            }

            if (!string.IsNullOrEmpty(operatingSystemFilter))
            {
                query = query.Where(s => s.OperatingSystem != null && s.OperatingSystem.Contains(operatingSystemFilter));
            }

            if (!string.IsNullOrEmpty(serverNameFilter))
            {
                query = query.Where(s => s.ServerName.Contains(serverNameFilter));
            }

            var totalCount = await query.CountAsync();
            var servers = await query
                .OrderBy(s => s.ServerName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            var viewModel = new ServerListViewModel
            {
                Servers = servers,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = PageSize,
                EnvironmentFilter = environmentFilter,
                ProjectFilter = projectFilter,
                OperatingSystemFilter = operatingSystemFilter,
                ServerNameFilter = serverNameFilter
            };

            return View(viewModel);
        }

        // GET: Server/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var server = await _context.Servers
                .Include(s => s.ServerServices)
                    .ThenInclude(ss => ss.Service)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (server == null)
            {
                return NotFound();
            }

            // Get installed KBs for this server
            var installedKbs = await _context.ServerInstalledKbs
                .Where(kb => kb.Computer == server.ServerName)
                .ToListAsync();

            // Get installed services
            var installedServices = server.ServerServices.Select(ss => ss.Service).ToList();

            // Calculate CVE exposures
            var cveExposures = await CalculateCveExposuresAsync(server, installedKbs);

            var viewModel = new ServerDetailsViewModel
            {
                Server = server,
                InstalledKbs = installedKbs,
                InstalledServices = installedServices,
                CveExposures = cveExposures,
                ComplianceSummary = CalculateComplianceSummary(cveExposures)
            };

            // Log server details access
            var currentUser = User.Identity?.Name ?? "Anonymous";
            var sourceIP = GetClientIpAddress();
            
            await _fileLoggingService.LogActionAsync(
                "Server Details Access", 
                currentUser, 
                $"Viewed details for server: {server.ServerName}", 
                sourceIP);

            return View(viewModel);
        }

        // GET: Server/Import - Admin only
        [Authorize(Roles = "Admin")]
        public IActionResult Import()
        {
            return View(new ServerImportViewModel());
        }

        // POST: Server/Import - Admin only
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Import(IFormFile csvFile)
        {
            var viewModel = new ServerImportViewModel
            {
                ImportStartTime = DateTime.UtcNow,
                FileName = csvFile?.FileName
            };

            if (csvFile == null || csvFile.Length == 0)
            {
                viewModel.ErrorMessage = "Please select a CSV file to upload.";
                viewModel.ExitCode = 1;
                return View(viewModel);
            }

            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                viewModel.ErrorMessage = "Please upload a CSV file.";
                viewModel.ExitCode = 1;
                return View(viewModel);
            }

            try
            {
                using var reader = new StreamReader(csvFile.OpenReadStream());
                var content = await reader.ReadToEndAsync();
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length < 2)
                {
                    viewModel.ErrorMessage = "CSV file must contain at least a header row and one data row.";
                    return View(viewModel);
                }

                var headers = ParseCsvLine(lines[0]);
                var serverNameIndex = FindColumnIndex(headers, "ServerName");
                
                // Also check for "VM" column (CSV1 format)
                if (serverNameIndex == -1)
                {
                    serverNameIndex = FindColumnIndex(headers, "VM");
                }

                if (serverNameIndex == -1)
                {
                    viewModel.ErrorMessage = "CSV file must contain a 'ServerName' or 'VM' column.";
                    return View(viewModel);
                }

                var importedCount = 0;
                var updatedCount = 0;
                var errors = new List<string>();
                var conflicts = new List<ServerImportConflict>();

                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var values = ParseCsvLine(lines[i]);
                        if (values.Length > serverNameIndex && !string.IsNullOrEmpty(values[serverNameIndex]))
                        {
                            var result = await ProcessServerRecord(headers, values, serverNameIndex);
                            
                            if (result.IsNewServer)
                            {
                                importedCount++;
                            }
                            else if (result.WasUpdated)
                            {
                                updatedCount++;
                            }

                            conflicts.AddRange(result.Conflicts);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Line {i + 1}: {ex.Message}");
                    }
                }

                // Save changes
                await _context.SaveChangesAsync();

                viewModel.ImportedCount = importedCount;
                viewModel.UpdatedCount = updatedCount;
                viewModel.Conflicts = conflicts;
                viewModel.ConflictCount = conflicts.Count;
                viewModel.Warnings = errors;
                viewModel.ErrorCount = errors.Count;
                viewModel.TotalRecordsProcessed = importedCount + updatedCount;
                viewModel.ImportEndTime = DateTime.UtcNow;
                viewModel.ExitCode = errors.Any() ? 1 : 0;

                if (conflicts.Any())
                {
                    viewModel.SuccessMessage = $"Import completed with conflicts. New servers: {importedCount}, Updated servers: {updatedCount}, Conflicts: {conflicts.Count}";
                }
                else
                {
                    viewModel.SuccessMessage = $"Import completed successfully! New servers: {importedCount}, Updated servers: {updatedCount}";
                }

                // Log import action
                var currentUser = User.Identity?.Name ?? "Anonymous";
                var sourceIP = GetClientIpAddress();
                
                await _fileLoggingService.LogActionAsync(
                    "Server CSV Import", 
                    currentUser, 
                    $"Imported server data: {importedCount} new, {updatedCount} updated, {conflicts.Count} conflicts", 
                    sourceIP);
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = $"Error processing CSV file: {ex.Message}";
                viewModel.ImportEndTime = DateTime.UtcNow;
                viewModel.ExitCode = 1;
                viewModel.ErrorCount = 1;
                _logger.LogError(ex, "Error importing server CSV file");
            }

            return View(viewModel);
        }

        private async Task<IEnumerable<CveExposureItem>> CalculateCveExposuresAsync(Server server, IEnumerable<ServerInstalledKb> installedKbs)
        {
            var exposures = new List<CveExposureItem>();

            if (string.IsNullOrEmpty(server.OperatingSystem))
            {
                return exposures;
            }

            // Get CVEs that might affect this server based on OS
            var relevantCves = await _context.CveUpdateStagings
                .Where(cve => cve.Product != null && 
                             (cve.Product.Contains("Windows") || cve.ProductFamily != null && cve.ProductFamily.Contains("Windows")))
                .ToListAsync();

            var installedKbList = installedKbs.Select(kb => kb.KB).ToHashSet();

            foreach (var cve in relevantCves)
            {
                var requiredKbs = ExtractKbsFromArticle(cve.Article);
                if (requiredKbs.Any())
                {
                    var missingKbs = requiredKbs.Where(kb => !installedKbList.Contains(kb)).ToList();
                    
                    exposures.Add(new CveExposureItem
                    {
                        CveId = cve.Details ?? "Unknown",
                        ProductFamily = cve.ProductFamily ?? "",
                        Product = cve.Product ?? "",
                        MaxSeverity = cve.MaxSeverity ?? "",
                        Article = cve.Article ?? "",
                        RequiredKbs = requiredKbs,
                        MissingKbs = missingKbs,
                        IsCompliant = !missingKbs.Any(),
                        ReleaseDate = cve.ReleaseDate
                    });
                }
            }

            return exposures.OrderByDescending(e => GetSeverityWeight(e.MaxSeverity)).ToList();
        }

        private ServerComplianceSummary CalculateComplianceSummary(IEnumerable<CveExposureItem> exposures)
        {
            var summary = new ServerComplianceSummary
            {
                TotalCveExposures = exposures.Count(),
                CompliantCves = exposures.Count(e => e.IsCompliant),
                NonCompliantCves = exposures.Count(e => !e.IsCompliant)
            };

            foreach (var exposure in exposures.Where(e => !e.IsCompliant))
            {
                switch (exposure.MaxSeverity?.ToLower())
                {
                    case "critical":
                        summary.CriticalExposures++;
                        break;
                    case "important":
                    case "high":
                        summary.HighExposures++;
                        break;
                    case "moderate":
                    case "medium":
                        summary.MediumExposures++;
                        break;
                    case "low":
                        summary.LowExposures++;
                        break;
                }
            }

            return summary;
        }

        private async Task<ServerImportResult> ProcessServerRecord(string[] headers, string[] values, int serverNameIndex)
        {
            var serverName = values[serverNameIndex].Trim();
            var existingServer = await _context.Servers.FirstOrDefaultAsync(s => s.ServerName == serverName);
            
            var result = new ServerImportResult
            {
                ServerName = serverName,
                IsNewServer = existingServer == null
            };

            if (existingServer == null)
            {
                // Create new server
                var newServer = new Server { ServerName = serverName };
                UpdateServerFromCsv(newServer, headers, values);
                _context.Servers.Add(newServer);
                result.WasUpdated = true;
            }
            else
            {
                // Update existing server, checking for conflicts
                var conflicts = CheckForConflicts(existingServer, headers, values);
                result.Conflicts = conflicts;
                
                // Apply updates (for now, we'll use new values, but this could be made configurable)
                UpdateServerFromCsv(existingServer, headers, values);
                result.WasUpdated = true;
            }

            return result;
        }

        private void UpdateServerFromCsv(Server server, string[] headers, string[] values)
        {
            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                var header = headers[i].Trim();
                var value = values[i].Trim();

                if (string.IsNullOrEmpty(value)) continue;

                switch (header.ToLower())
                {
                    case "vcenter":
                        server.VCenter = value;
                        break;
                    case "cluster":
                        server.Cluster = value;
                        break;
                    case "vm":
                    case "servername":
                        // Server name is handled separately, but we can update it here if needed
                        if (string.IsNullOrEmpty(server.ServerName))
                            server.ServerName = value;
                        break;
                    case "os":
                    case "operatingsystem":
                        server.OperatingSystem = value;
                        break;
                    case "project":
                        server.Project = value;
                        break;
                    case "environment":
                        server.Environment = value;
                        break;
                    case "idel":
                        server.IDEL = value;
                        break;
                    case "serverip":
                        server.ServerIP = value;
                        break;
                    case "managementip":
                    case "serverip (mgt)":
                        server.ManagementIP = value;
                        break;
                    case "operatingsystemversion":
                        server.OperatingSystemVersion = value;
                        break;
                    case "lastboottime":
                        if (DateTime.TryParse(value, out var bootTime))
                            server.LastBootTime = bootTime;
                        break;
                    case "localadmins":
                        server.LocalAdmins = value;
                        break;
                    case "osdisksize":
                        if (decimal.TryParse(value, out var diskSize))
                            server.OSDiskSize = diskSize;
                        break;
                    case "osdiskfree":
                        if (decimal.TryParse(value, out var diskFree))
                            server.OSDiskFree = diskFree;
                        break;
                    case "serviceowner":
                    case "service_owner":
                        server.ServiceOwner = value;
                        break;
                    case "maintenancewindows":
                        server.MaintenanceWindows = value;
                        break;
                    case "status":
                        server.Status = value;
                        break;
                    case "build":
                    case "osversion":
                        server.Build = value;
                        break;
                    case "cynetlauncher":
                    case "wincollect":
                    case "s1agent":
                    case "s1selfprotection":
                    case "s1monitorbuildid":
                    case "s1mgmtserver":
                        // Append service info to Services field
                        var serviceName = header.ToLower() switch
                        {
                            "cynetlauncher" => "CynetLauncher",
                            "wincollect" => "Wincollect",
                            "s1agent" => "S1Agent",
                            "s1selfprotection" => "S1SelfProtection",
                            "s1monitorbuildid" => "S1MonitorBuildId",
                            "s1mgmtserver" => "S1MgmtServer",
                            _ => header
                        };
                        
                        if (!string.IsNullOrEmpty(server.Services))
                        {
                            if (!server.Services.Contains(serviceName))
                                server.Services += $"; {serviceName}: {value}";
                        }
                        else
                        {
                            server.Services = $"{serviceName}: {value}";
                        }
                        break;
                }
            }

            server.LastUpdated = DateTime.UtcNow;
        }

        private List<ServerImportConflict> CheckForConflicts(Server existingServer, string[] headers, string[] values)
        {
            var conflicts = new List<ServerImportConflict>();

            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                var header = headers[i].Trim();
                var newValue = values[i].Trim();

                if (string.IsNullOrEmpty(newValue)) continue;

                string? existingValue = null;
                bool hasConflict = false;

                switch (header.ToLower())
                {
                    case "vcenter":
                        existingValue = existingServer.VCenter;
                        hasConflict = !string.IsNullOrEmpty(existingValue) && existingValue != newValue;
                        break;
                    case "cluster":
                        existingValue = existingServer.Cluster;
                        hasConflict = !string.IsNullOrEmpty(existingValue) && existingValue != newValue;
                        break;
                    case "environment":
                        existingValue = existingServer.Environment;
                        hasConflict = !string.IsNullOrEmpty(existingValue) && existingValue != newValue;
                        break;
                    case "serverip":
                        existingValue = existingServer.ServerIP;
                        hasConflict = !string.IsNullOrEmpty(existingValue) && existingValue != newValue;
                        break;
                    // Add other fields as needed
                }

                if (hasConflict)
                {
                    conflicts.Add(new ServerImportConflict
                    {
                        ServerName = existingServer.ServerName,
                        Field = header,
                        ExistingValue = existingValue,
                        NewValue = newValue,
                        Resolution = ConflictResolution.UseNew // Default resolution
                    });
                }
            }

            return conflicts;
        }

        private int FindColumnIndex(string[] headers, string columnName)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private string[] ParseCsvLine(string line)
        {
            // Simple CSV parsing - handles quoted fields
            var result = new List<string>();
            var current = "";
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current);

            return result.ToArray();
        }

        private List<string> ExtractKbsFromArticle(string? article)
        {
            var kbs = new List<string>();
            if (string.IsNullOrEmpty(article)) return kbs;

            // Extract KB numbers from the article field
            var kbPattern = @"KB\d{6,7}";
            var matches = System.Text.RegularExpressions.Regex.Matches(article, kbPattern);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (!kbs.Contains(match.Value))
                {
                    kbs.Add(match.Value);
                }
            }

            return kbs;
        }

        private int GetSeverityWeight(string? severity)
        {
            return severity?.ToLower() switch
            {
                "critical" => 4,
                "important" or "high" => 3,
                "moderate" or "medium" => 2,
                "low" => 1,
                _ => 0
            };
        }

        private string GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private class ServerImportResult
        {
            public string ServerName { get; set; } = string.Empty;
            public bool IsNewServer { get; set; }
            public bool WasUpdated { get; set; }
            public List<ServerImportConflict> Conflicts { get; set; } = new List<ServerImportConflict>();
        }
    }
}