using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// View model for server listing and dashboard
    /// </summary>
    public class ServerListViewModel
    {
        public IEnumerable<Server> Servers { get; set; } = new List<Server>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        // Filter properties
        public string? EnvironmentFilter { get; set; }
        public string? ProjectFilter { get; set; }
        public string? OperatingSystemFilter { get; set; }
        public string? ServerNameFilter { get; set; }
    }

    /// <summary>
    /// View model for server details with CVE exposure and installed KBs
    /// </summary>
    public class ServerDetailsViewModel
    {
        public Server Server { get; set; } = null!;
        public IEnumerable<ServerInstalledKb> InstalledKbs { get; set; } = new List<ServerInstalledKb>();
        public IEnumerable<Service> InstalledServices { get; set; } = new List<Service>();
        public IEnumerable<CveExposureItem> CveExposures { get; set; } = new List<CveExposureItem>();
        public ServerComplianceSummary ComplianceSummary { get; set; } = new ServerComplianceSummary();
    }

    /// <summary>
    /// Represents CVE exposure for a specific server
    /// </summary>
    public class CveExposureItem
    {
        public string CveId { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string MaxSeverity { get; set; } = string.Empty;
        public string Article { get; set; } = string.Empty;
        public List<string> RequiredKbs { get; set; } = new List<string>();
        public List<string> MissingKbs { get; set; } = new List<string>();
        public bool IsCompliant { get; set; }
        public DateTime? ReleaseDate { get; set; }
    }

    /// <summary>
    /// Server compliance summary
    /// </summary>
    public class ServerComplianceSummary
    {
        public int TotalCveExposures { get; set; }
        public int CompliantCves { get; set; }
        public int NonCompliantCves { get; set; }
        public int CriticalExposures { get; set; }
        public int HighExposures { get; set; }
        public int MediumExposures { get; set; }
        public int LowExposures { get; set; }
    }

    /// <summary>
    /// View model for server CSV import
    /// </summary>
    public class ServerImportViewModel
    {
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<ServerImportConflict> Conflicts { get; set; } = new List<ServerImportConflict>();
        public int ImportedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ConflictCount { get; set; }
    }

    /// <summary>
    /// Represents a conflict during server import
    /// </summary>
    public class ServerImportConflict
    {
        public string ServerName { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string? ExistingValue { get; set; }
        public string? NewValue { get; set; }
        public ConflictResolution Resolution { get; set; } = ConflictResolution.KeepExisting;
    }

    /// <summary>
    /// Options for resolving import conflicts
    /// </summary>
    public enum ConflictResolution
    {
        KeepExisting,
        UseNew,
        Skip
    }
}