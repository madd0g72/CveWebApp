using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CveWebApp.Data;
using CveWebApp.Models;
using System.Globalization;

namespace CveWebApp.Controllers
{
    /// <summary>
    /// Controller for VMWare data import functionality - restricted to Admin role only
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class VMWareImportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VMWareImportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: VMWareImport
        public IActionResult Index()
        {
            return View();
        }

        // POST: VMWareImport/UploadServers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadServers(IFormFile? csvFile)
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
                var importResult = await ProcessServersCsvFileAsync(csvFile);
                importResult.FileName = csvFile.FileName;
                importResult.ExitCode = importResult.IsSuccessful ? 0 : 1;

                ViewBag.ImportResult = importResult;
                ViewBag.ImportStartTime = DateTime.UtcNow;
                ViewBag.ImportEndTime = DateTime.UtcNow;

                return View("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error processing file: {ex.Message}");
                return View("Index");
            }
        }

        // POST: VMWareImport/UploadNetworks
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadNetworks(IFormFile? csvFile)
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
                var importResult = await ProcessNetworksCsvFileAsync(csvFile);
                importResult.FileName = csvFile.FileName;
                importResult.ExitCode = importResult.IsSuccessful ? 0 : 1;

                ViewBag.ImportResult = importResult;
                ViewBag.ImportStartTime = DateTime.UtcNow;
                ViewBag.ImportEndTime = DateTime.UtcNow;

                return View("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error processing file: {ex.Message}");
                return View("Index");
            }
        }

        private async Task<VMWareImportResult> ProcessServersCsvFileAsync(IFormFile csvFile)
        {
            var result = new VMWareImportResult { IsSuccessful = false };
            var servers = new List<VMWareServerList>();

            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream);

            // Read header line
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
            {
                throw new InvalidOperationException("CSV file is empty or missing header row.");
            }

            var headers = ParseCsvLine(headerLine);
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
                    if (columns.Length != headers.Length)
                    {
                        result.Errors.Add($"Line {lineNumber}: Column count mismatch. Expected {headers.Length}, got {columns.Length}");
                        continue;
                    }

                    var server = new VMWareServerList();

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var header = NormalizeHeader(headers[i]);
                        var value = columns[i];

                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        try
                        {
                            MapServerField(server, header, value);
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Line {lineNumber}, Column '{header}': {ex.Message}");
                        }
                    }

                    servers.Add(server);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                }
            }

            // Save to database
            if (servers.Any())
            {
                _context.VMWareServerLists.AddRange(servers);
                await _context.SaveChangesAsync();
                result.RecordsImported = servers.Count;
            }

            result.IsSuccessful = !result.Errors.Any();
            return result;
        }

        private async Task<VMWareImportResult> ProcessNetworksCsvFileAsync(IFormFile csvFile)
        {
            var result = new VMWareImportResult { IsSuccessful = false };
            var networks = new List<VMWareServersNetworksList>();

            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream);

            // Read header line
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
            {
                throw new InvalidOperationException("CSV file is empty or missing header row.");
            }

            var headers = ParseCsvLine(headerLine);
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
                    if (columns.Length != headers.Length)
                    {
                        result.Errors.Add($"Line {lineNumber}: Column count mismatch. Expected {headers.Length}, got {columns.Length}");
                        continue;
                    }

                    var network = new VMWareServersNetworksList();

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var header = NormalizeHeader(headers[i]);
                        var value = columns[i];

                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        try
                        {
                            MapNetworkField(network, header, value);
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Line {lineNumber}, Column '{header}': {ex.Message}");
                        }
                    }

                    networks.Add(network);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Line {lineNumber}: {ex.Message}");
                }
            }

            // Save to database
            if (networks.Any())
            {
                _context.VMWareServersNetworksLists.AddRange(networks);
                await _context.SaveChangesAsync();
                result.RecordsImported = networks.Count;
            }

            result.IsSuccessful = !result.Errors.Any();
            return result;
        }

        private void MapServerField(VMWareServerList server, string header, string value)
        {
            switch (header.ToLowerInvariant())
            {
                case "service":
                    server.Service = value;
                    break;
                case "location":
                    server.Location = value;
                    break;
                case "region":
                    server.Region = value;
                    break;
                case "vcenter":
                    server.VCenter = value;
                    break;
                case "cluster":
                    server.Cluster = value;
                    break;
                case "vm":
                    server.VM = value;
                    break;
                case "esxi":
                    server.ESXi = value;
                    break;
                case "status":
                    server.Status = value;
                    break;
                case "os":
                    server.OS = value;
                    break;
                case "dnsname":
                    server.DnsName = value;
                    break;
                case "totcpu":
                    if (int.TryParse(value, out int totCpu))
                        server.TotCPU = totCpu;
                    break;
                case "socket":
                    if (int.TryParse(value, out int socket))
                        server.Socket = socket;
                    break;
                case "core":
                    if (int.TryParse(value, out int core))
                        server.Core = core;
                    break;
                case "vmem":
                    if (long.TryParse(value, out long vmem))
                        server.VMem = vmem;
                    break;
                case "sizegb":
                    if (decimal.TryParse(value, out decimal sizeGb))
                        server.SizeGB = sizeGb;
                    break;
                case "backupveeam":
                    server.BackupVeeam = value;
                    break;
                case "vhw":
                    server.VHW = value;
                    break;
                case "tools_status":
                case "toolsstatus":
                    server.ToolsStatus = value;
                    break;
                case "tools_version":
                case "toolsversion":
                    server.ToolsVersion = value;
                    break;
                case "ip":
                    server.IP = value;
                    break;
                case "vmx":
                    server.VMX = value;
                    break;
                case "folder":
                    server.Folder = value;
                    break;
                case "rp":
                    server.RP = value;
                    break;
                case "scope":
                    server.SCOPE = value;
                    break;
                case "sub_scope":
                case "subscope":
                    server.SUB_SCOPE = value;
                    break;
                case "customer":
                    server.CUSTOMER = value;
                    break;
                case "owner":
                    server.Owner = value;
                    break;
                case "group":
                    server.Group = value;
                    break;
                case "service_owner":
                case "serviceowner":
                    server.ServiceOwner = value;
                    break;
                case "service_group":
                case "servicegroup":
                    server.ServiceGroup = value;
                    break;
                case "project":
                    server.Project = value;
                    break;
                case "creationdate":
                    if (DateTime.TryParse(value, out DateTime creationDate))
                        server.CreationDate = creationDate;
                    break;
                case "vmcreationdate":
                    if (DateTime.TryParse(value, out DateTime vmCreationDate))
                        server.VMCreationDate = vmCreationDate;
                    break;
                case "functionality":
                    server.Functionality = value;
                    break;
                case "deploy_from":
                case "deployfrom":
                    server.DeployFrom = value;
                    break;
                case "idel":
                    server.IDEL = value;
                    break;
                case "environment":
                    server.Environment = value;
                    break;
                case "priority":
                    server.Priority = value;
                    break;
                case "note":
                    server.Note = value;
                    break;
            }
        }

        private void MapNetworkField(VMWareServersNetworksList network, string header, string value)
        {
            switch (header.ToLowerInvariant())
            {
                case "service":
                    network.Service = value;
                    break;
                case "location":
                    network.Location = value;
                    break;
                case "region":
                    network.Region = value;
                    break;
                case "vcenter":
                    network.VCenter = value;
                    break;
                case "cluster":
                    network.Cluster = value;
                    break;
                case "vmname":
                    network.VmName = value;
                    break;
                case "os":
                    network.OS = value;
                    break;
                case "status":
                    network.Status = value;
                    break;
                case "owner":
                    network.Owner = value;
                    break;
                case "tools":
                    network.Tools = value;
                    break;
                case "macaddress":
                    network.MacAddress = value;
                    break;
                case "ipaddress":
                    network.IpAddress = value;
                    break;
                case "connected":
                    if (bool.TryParse(value, out bool connected))
                        network.Connected = connected;
                    else if (value.Equals("True", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase))
                        network.Connected = true;
                    else if (value.Equals("False", StringComparison.OrdinalIgnoreCase) || value.Equals("0", StringComparison.OrdinalIgnoreCase))
                        network.Connected = false;
                    break;
                case "portgroup":
                    network.PortGroup = value;
                    break;
                case "type":
                    network.Type = value;
                    break;
            }
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
        /// <returns>Normalized header string</returns>
        private static string NormalizeHeader(string header)
        {
            return header?.Trim('"', ' ') ?? string.Empty;
        }
    }

    /// <summary>
    /// Result object for VMWare import operations
    /// </summary>
    public class VMWareImportResult
    {
        public bool IsSuccessful { get; set; }
        public int RecordsImported { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string? FileName { get; set; }
        public int ExitCode { get; set; }
    }
}