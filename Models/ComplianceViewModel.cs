using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    public class ComplianceViewModel
    {
        public CveUpdateStaging CveDetails { get; set; } = new();
        public List<ServerComplianceStatus> ServerStatuses { get; set; } = new();
        public ComplianceSummary Summary { get; set; } = new();
        public List<string> RequiredKbs { get; set; } = new();
    }

    public class CveWithCompliance
    {
        public CveUpdateStaging Cve { get; set; } = new();
        public double CompliancePercentage { get; set; }
    }

    public class ServerComplianceStatus
    {
        public string Computer { get; set; } = string.Empty;
        public string OSProduct { get; set; } = string.Empty;
        public List<string> InstalledKbs { get; set; } = new();
        public List<string> MissingKbs { get; set; } = new();
        public bool IsCompliant { get; set; }
        public List<SupersedenceInfo> SupersedenceDetails { get; set; } = new();
    }

    public class SupersedenceInfo
    {
        public string RequiredKb { get; set; } = string.Empty;
        public string SupersedingKb { get; set; } = string.Empty;
        public string ComplianceReason { get; set; } = string.Empty; // "Direct KB" or "Superseding KB"
    }

    public class ComplianceSummary
    {
        public int TotalServers { get; set; }
        public int CompliantServers { get; set; }
        public int NonCompliantServers { get; set; }
        public double CompliancePercentage => TotalServers > 0 ? (CompliantServers / (double)TotalServers) * 100 : 0;
    }
}