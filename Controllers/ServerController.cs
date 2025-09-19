using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using CveWebApp.Services;
using Microsoft.AspNetCore.Authorization;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for managing server assets with data combined from VMWare tables
    /// </summary>
    public class ServerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServerController> _logger;

        public ServerController(ApplicationDbContext context, ILogger<ServerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display the server assets index page with filtering
        /// </summary>
        public async Task<IActionResult> Index(
            string? serverNameFilter,
            string? environmentFilter,
            string? projectFilter,
            string? operatingSystemFilter,
            int pageNumber = 1,
            int pageSize = 25)
        {
            var query = _context.Servers.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(serverNameFilter))
            {
                query = query.Where(s => s.ServerName.Contains(serverNameFilter));
            }

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

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply pagination and ordering
            var servers = await query
                .OrderBy(s => s.ServerName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get servers that have KB data for WSUS status display
            // Normalize server names for proper matching (remove domain suffixes)
            var normalizedServerNames = servers.Select(s => ServerNameHelper.NormalizeServerName(s.ServerName)).ToList();
            var serversWithKbData = await _context.ServerInstalledKbs
                .Select(kb => kb.Computer)
                .Distinct()
                .ToListAsync();

            // Update WSUS status for servers with KB data
            foreach (var server in servers)
            {
                var normalizedServerName = ServerNameHelper.NormalizeServerName(server.ServerName);
                var hasKbData = serversWithKbData.Any(kbComputer => 
                    ServerNameHelper.DoServerNamesMatch(normalizedServerName, kbComputer));
                
                if (hasKbData && server.WSUSStatus == "Unknown")
                {
                    server.WSUSStatus = "Connected";
                }
            }

            var viewModel = new ServerListViewModel
            {
                Servers = servers,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                ServerNameFilter = serverNameFilter,
                EnvironmentFilter = environmentFilter,
                ProjectFilter = projectFilter,
                OperatingSystemFilter = operatingSystemFilter
            };

            return View(viewModel);
        }

        /// <summary>
        /// Display detailed information for a specific server
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var server = await _context.Servers
                .FirstOrDefaultAsync(s => s.Id == id);

            if (server == null)
            {
                return NotFound();
            }

            // Get installed KBs by matching server name with Computer field (normalized for domain compatibility)
            var normalizedServerName = ServerNameHelper.NormalizeServerName(server.ServerName);
            
            // First try exact match for performance
            var installedKbs = await _context.ServerInstalledKbs
                .Where(kb => kb.Computer == server.ServerName)
                .ToListAsync();
                
            // If no exact match found, try normalized matching
            if (!installedKbs.Any())
            {
                var allKbs = await _context.ServerInstalledKbs.ToListAsync();
                installedKbs = allKbs
                    .Where(kb => ServerNameHelper.DoServerNamesMatch(normalizedServerName, kb.Computer))
                    .ToList();
            }

            // Update WSUS status based on presence of KB data
            if (installedKbs.Any() && server.WSUSStatus == "Unknown")
            {
                server.WSUSStatus = "Connected";
                _context.Servers.Update(server);
                await _context.SaveChangesAsync();
            }

            // Get installed services (if any) - for now, use empty list as this isn't the focus of the fix
            var installedServices = new List<Service>();

            // Calculate CVE exposures and compliance summary
            var cveExposures = await CalculateCveExposuresAsync(server, installedKbs);
            var complianceSummary = CalculateComplianceSummary(cveExposures);

            var viewModel = new ServerDetailsViewModel
            {
                Server = server,
                InstalledKbs = installedKbs,
                InstalledServices = installedServices,
                CveExposures = cveExposures,
                ComplianceSummary = complianceSummary
            };

            return View(viewModel);
        }

        /// <summary>
        /// Import/refresh server data from VMWare tables (Admin only)
        /// </summary>
        [Authorize(Roles = "Admin")]
        public IActionResult Import()
        {
            var viewModel = new ServerImportViewModel();
            return View(viewModel);
        }

        /// <summary>
        /// Process server data import from VMWare tables
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportFromVMWare()
        {
            try
            {
                var importResult = await ImportServersFromVMWareAsync();
                return View("Import", importResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing servers from VMWare tables");
                var errorModel = new ServerImportViewModel
                {
                    ErrorMessage = $"Import failed: {ex.Message}",
                    ExitCode = 1
                };
                return View("Import", errorModel);
            }
        }

        /// <summary>
        /// Add sample test data for demonstrating CVE exposure calculation (Development only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTestData()
        {
            try
            {
                // Only allow in development environment
                if (!_context.Database.IsInMemory())
                {
                    TempData["ErrorMessage"] = "Test data can only be added in development environment.";
                    return RedirectToAction("Index");
                }

                // Add test CVE data
                var testCves = new List<CveUpdateStaging>
                {
                    new CveUpdateStaging
                    {
                        ReleaseDate = DateTime.UtcNow.AddDays(-30),
                        ProductFamily = "Windows",
                        Product = "Windows Server 2019",
                        Platform = "x64",
                        Impact = "Remote Code Execution",
                        MaxSeverity = "Critical",
                        Article = "KB5008123: Security Update for Windows Server 2019",
                        Details = "CVE-2023-1234",
                        BaseScore = 9.8m,
                        CustomerActionRequired = true
                    },
                    new CveUpdateStaging
                    {
                        ReleaseDate = DateTime.UtcNow.AddDays(-15),
                        ProductFamily = "Windows", 
                        Product = "Windows Server 2019",
                        Platform = "x64",
                        Impact = "Elevation of Privilege",
                        MaxSeverity = "Important",
                        Article = "KB5009456: Security Update for Windows Server 2019",
                        Details = "CVE-2023-5678",
                        BaseScore = 7.5m,
                        CustomerActionRequired = true
                    },
                    new CveUpdateStaging
                    {
                        ReleaseDate = DateTime.UtcNow.AddDays(-45),
                        ProductFamily = "Windows",
                        Product = "Windows Server 2022", 
                        Platform = "x64",
                        Impact = "Information Disclosure",
                        MaxSeverity = "Important",
                        Article = "KB5007789: Security Update for Windows Server 2022",
                        Details = "CVE-2023-9012",
                        BaseScore = 6.5m,
                        CustomerActionRequired = true
                    },
                    // Additional test CVEs to demonstrate the broad matching issue
                    new CveUpdateStaging
                    {
                        ReleaseDate = DateTime.UtcNow.AddDays(-10),
                        ProductFamily = "Windows",
                        Product = "Windows", // Generic - should NOT match servers specifically
                        Platform = "x64",
                        Impact = "Remote Code Execution",
                        MaxSeverity = "Critical",
                        Article = "KB5001234: Security Update for Windows",
                        Details = "CVE-2023-GENERIC",
                        BaseScore = 8.8m,
                        CustomerActionRequired = true
                    },
                    new CveUpdateStaging
                    {
                        ReleaseDate = DateTime.UtcNow.AddDays(-5),
                        ProductFamily = "Windows",
                        Product = "Windows 10", // Desktop OS - should NOT match servers
                        Platform = "x64",
                        Impact = "Elevation of Privilege",
                        MaxSeverity = "Important",
                        Article = "KB5002345: Security Update for Windows 10",
                        Details = "CVE-2023-WIN10",
                        BaseScore = 7.0m,
                        CustomerActionRequired = true
                    },
                    new CveUpdateStaging
                    {
                        ReleaseDate = DateTime.UtcNow.AddDays(-20),
                        ProductFamily = "Windows",
                        Product = "Windows Server", // Vague - no version specified
                        Platform = "x64",
                        Impact = "Information Disclosure",
                        MaxSeverity = "Important",
                        Article = "KB5003456: Security Update for Windows Server",
                        Details = "CVE-2023-WINSVR",
                        BaseScore = 6.5m,
                        CustomerActionRequired = true
                    }
                };

                // Add test KB installation data for web-server-01
                var testKbs = new List<ServerInstalledKb>
                {
                    new ServerInstalledKb
                    {
                        Computer = "web-server-01",
                        OSProduct = "Microsoft Windows Server 2019 Standard", 
                        KB = "KB5008123" // This KB will make the server compliant for CVE-2023-1234
                    },
                    new ServerInstalledKb
                    {
                        Computer = "web-server-01",
                        OSProduct = "Microsoft Windows Server 2019 Standard",
                        KB = "KB5007000" // An unrelated KB
                    }
                    // Note: Missing KB5009456, so server will be non-compliant for CVE-2023-5678
                };

                // Add test KB installation data for db-server-01  
                var testKbsDb = new List<ServerInstalledKb>
                {
                    new ServerInstalledKb
                    {
                        Computer = "db-server-01", 
                        OSProduct = "Microsoft Windows Server 2022 Standard",
                        KB = "KB5007789" // This KB will make the server compliant for CVE-2023-9012
                    }
                };

                // Add to database
                await _context.CveUpdateStagings.AddRangeAsync(testCves);
                await _context.ServerInstalledKbs.AddRangeAsync(testKbs);
                await _context.ServerInstalledKbs.AddRangeAsync(testKbsDb);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Added {testCves.Count} test CVE records and {testKbs.Count + testKbsDb.Count} test KB installations.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding test data");
                TempData["ErrorMessage"] = $"Failed to add test data: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Combines data from VMWareServerList and VMWareServersNetworksList to populate Server table
        /// </summary>
        private async Task<ServerImportViewModel> ImportServersFromVMWareAsync()
        {
            var startTime = DateTime.UtcNow;
            var result = new ServerImportViewModel
            {
                ImportStartTime = startTime
            };

            try
            {
                // Get all VMWare server data
                var vmwareServers = await _context.VMWareServerLists.ToListAsync();
                var vmwareNetworks = await _context.VMWareServersNetworksLists.ToListAsync();

                int importedCount = 0;
                int updatedCount = 0;

                foreach (var vmServer in vmwareServers)
                {
                    if (string.IsNullOrEmpty(vmServer.VM))
                        continue;

                    // Find matching network entries for this server
                    var serverNetworks = vmwareNetworks
                        .Where(n => n.VmName == vmServer.VM)
                        .ToList();

                    // Combine IP addresses and port groups
                    var ipAddresses = serverNetworks
                        .Where(n => !string.IsNullOrEmpty(n.IpAddress))
                        .Select(n => n.IpAddress!)
                        .Distinct()
                        .ToList();

                    var portGroups = serverNetworks
                        .Where(n => !string.IsNullOrEmpty(n.PortGroup))
                        .Select(n => n.PortGroup!)
                        .Distinct()
                        .ToList();

                    // Check if server already exists
                    var existingServer = await _context.Servers
                        .FirstOrDefaultAsync(s => s.ServerName == vmServer.VM);

                    if (existingServer == null)
                    {
                        // Create new server
                        var newServer = new Server
                        {
                            ServerName = vmServer.VM,
                            VCenter = vmServer.VCenter,
                            Cluster = vmServer.Cluster,
                            Project = vmServer.Project,
                            Environment = vmServer.Environment,
                            IDEL = vmServer.IDEL,
                            OperatingSystem = vmServer.OS,
                            Status = vmServer.Status,
                            ServiceOwner = vmServer.ServiceOwner,
                            IPAddresses = string.Join(", ", ipAddresses),
                            PortGroups = string.Join(", ", portGroups),
                            // Set default status values for new servers
                            WSUSStatus = "Unknown",
                            ADStatus = "Unknown", 
                            AVStatus = "Unknown",
                            SIEMStatus = "Unknown",
                            CreatedDate = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow
                        };

                        _context.Servers.Add(newServer);
                        importedCount++;
                    }
                    else
                    {
                        // Update existing server
                        existingServer.VCenter = vmServer.VCenter;
                        existingServer.Cluster = vmServer.Cluster;
                        existingServer.Project = vmServer.Project;
                        existingServer.Environment = vmServer.Environment;
                        existingServer.IDEL = vmServer.IDEL;
                        existingServer.OperatingSystem = vmServer.OS;
                        existingServer.Status = vmServer.Status;
                        existingServer.ServiceOwner = vmServer.ServiceOwner;
                        existingServer.IPAddresses = string.Join(", ", ipAddresses);
                        existingServer.PortGroups = string.Join(", ", portGroups);
                        existingServer.LastUpdated = DateTime.UtcNow;

                        _context.Servers.Update(existingServer);
                        updatedCount++;
                    }
                }

                await _context.SaveChangesAsync();

                result.ImportedCount = importedCount;
                result.UpdatedCount = updatedCount;
                result.TotalRecordsProcessed = vmwareServers.Count;
                result.SuccessMessage = $"Successfully processed {vmwareServers.Count} VMWare servers. " +
                                       $"Created {importedCount} new servers, updated {updatedCount} existing servers.";
                result.ExitCode = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during VMWare server import");
                result.ErrorMessage = ex.Message;
                result.ExitCode = 1;
            }

            result.ImportEndTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Calculate CVE exposures for a specific server based on its operating system and installed KBs
        /// </summary>
        private async Task<List<CveExposureItem>> CalculateCveExposuresAsync(Server server, List<ServerInstalledKb> installedKbs)
        {
            var exposures = new List<CveExposureItem>();

            if (string.IsNullOrEmpty(server.OperatingSystem))
            {
                return exposures; // Cannot determine CVE exposures without OS information
            }

            // Get all CVE records that might apply to this server's operating system
            var applicableCves = await GetApplicableCvesAsync(server.OperatingSystem);

            foreach (var cve in applicableCves)
            {
                // Extract required KBs from the CVE article
                var requiredKbs = ExtractKbsFromArticle(cve.Article);

                if (requiredKbs.Count == 0)
                {
                    continue; // Skip CVEs without KB requirements
                }

                // Check compliance for this CVE
                var serverKbs = installedKbs.Select(kb => kb.KB).ToList();
                var complianceResult = await GetServerComplianceWithSupersedenceDetails(
                    requiredKbs, 
                    serverKbs, 
                    cve.Product, 
                    cve.ProductFamily);

                var exposure = new CveExposureItem
                {
                    CveId = cve.Details ?? "Unknown",
                    ProductFamily = cve.ProductFamily ?? "",
                    Product = cve.Product ?? "",
                    MaxSeverity = cve.MaxSeverity ?? "",
                    Article = cve.Article ?? "",
                    RequiredKbs = requiredKbs,
                    MissingKbs = complianceResult.MissingKbs,
                    IsCompliant = complianceResult.IsCompliant,
                    ReleaseDate = cve.ReleaseDate
                };

                exposures.Add(exposure);
            }

            return exposures;
        }

        /// <summary>
        /// Get CVE records that apply to the specified operating system
        /// </summary>
        private async Task<List<CveUpdateStaging>> GetApplicableCvesAsync(string operatingSystem)
        {
            // Match CVEs by product name contained in the operating system string
            // This handles cases like:
            // - OS: "Microsoft Windows Server 2019 Standard" -> matches Product: "Windows Server 2019"
            // - OS: "Microsoft Windows Server 2022 Standard" -> matches Product: "Windows Server 2022"
            
            var query = _context.CveUpdateStagings.AsQueryable();

            // Try to find CVEs where the Product is contained in the OS string
            var cves = await query
                .Where(c => !string.IsNullOrEmpty(c.Product) && 
                           operatingSystem.Contains(c.Product))
                .ToListAsync();

            // If no matches by Product, try ProductFamily
            if (!cves.Any())
            {
                cves = await query
                    .Where(c => !string.IsNullOrEmpty(c.ProductFamily) && 
                               operatingSystem.Contains(c.ProductFamily))
                    .ToListAsync();
            }

            // If still no matches, try broader Windows matching
            if (!cves.Any() && operatingSystem.ToLower().Contains("windows"))
            {
                cves = await query
                    .Where(c => (c.Product != null && c.Product.ToLower().Contains("windows")) ||
                               (c.ProductFamily != null && c.ProductFamily.ToLower().Contains("windows")))
                    .ToListAsync();
            }

            return cves;
        }

        /// <summary>
        /// Calculate compliance summary from CVE exposures
        /// </summary>
        private ServerComplianceSummary CalculateComplianceSummary(List<CveExposureItem> exposures)
        {
            var summary = new ServerComplianceSummary
            {
                TotalCveExposures = exposures.Count,
                CompliantCves = exposures.Count(e => e.IsCompliant),
                NonCompliantCves = exposures.Count(e => !e.IsCompliant)
            };

            // Calculate severity breakdown for non-compliant CVEs
            var nonCompliantExposures = exposures.Where(e => !e.IsCompliant).ToList();
            
            summary.CriticalExposures = nonCompliantExposures.Count(e => 
                string.Equals(e.MaxSeverity, "Critical", StringComparison.OrdinalIgnoreCase));
            
            summary.HighExposures = nonCompliantExposures.Count(e => 
                string.Equals(e.MaxSeverity, "Important", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.MaxSeverity, "High", StringComparison.OrdinalIgnoreCase));
            
            summary.MediumExposures = nonCompliantExposures.Count(e => 
                string.Equals(e.MaxSeverity, "Moderate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.MaxSeverity, "Medium", StringComparison.OrdinalIgnoreCase));
            
            summary.LowExposures = nonCompliantExposures.Count(e => 
                string.Equals(e.MaxSeverity, "Low", StringComparison.OrdinalIgnoreCase));

            return summary;
        }

        /// <summary>
        /// Extract KB numbers from article text - adapted from CveController
        /// </summary>
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

        /// <summary>
        /// Gets comprehensive compliance information including supersedence details - adapted from CveController
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
        /// Find superseding KB - simplified version for now
        /// </summary>
        private async Task<string?> FindSupersedingKbAsync(string requiredKb, List<string> installedKbs, string? product, string? productFamily)
        {
            // For now, return null - supersedence logic would require KB supersedence table
            // This can be enhanced later with proper supersedence checking
            return await Task.FromResult<string?>(null);
        }
    }
}