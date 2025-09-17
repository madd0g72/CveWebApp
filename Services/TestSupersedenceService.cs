using System.Globalization;
using CveWebApp.Data;
using CveWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CveWebApp.Services
{
    /// <summary>
    /// Service for adding test supersedence data for demonstration purposes
    /// </summary>
    public class TestSupersedenceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TestSupersedenceService> _logger;

        public TestSupersedenceService(ApplicationDbContext context, ILogger<TestSupersedenceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Adds a test server with superseding KB to demonstrate supersedence functionality
        /// </summary>
        public async Task AddTestSupersedenceServerAsync()
        {
            // Check if test server already exists
            var existingServer = await _context.ServerInstalledKbs
                .FirstOrDefaultAsync(s => s.Computer == "test-supersedence-server.lottomatica.net");

            if (existingServer != null)
            {
                _logger.LogInformation("Test supersedence server already exists");
                return;
            }

            // Add test server with KB5061010 which supersedes KB5058383
            var testServerKbs = new List<ServerInstalledKb>
            {
                new ServerInstalledKb 
                { 
                    Computer = "test-supersedence-server.lottomatica.net", 
                    OSProduct = "Windows Server 2016 Datacenter", 
                    KB = "925673" 
                },
                new ServerInstalledKb 
                { 
                    Computer = "test-supersedence-server.lottomatica.net", 
                    OSProduct = "Windows Server 2016 Datacenter", 
                    KB = "5061010" // This supersedes KB5058383
                }
            };

            _context.ServerInstalledKbs.AddRange(testServerKbs);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added test supersedence server with KB5061010 that supersedes KB5058383");
        }
    }
}