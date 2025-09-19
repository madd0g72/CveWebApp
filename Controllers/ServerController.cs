using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
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
                .Include(s => s.InstalledKbs)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (server == null)
            {
                return NotFound();
            }

            var viewModel = new ServerDetailsViewModel
            {
                Server = server
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
    }
}