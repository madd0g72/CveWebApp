using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Represents a VMWare server with infrastructure and operational information
    /// </summary>
    public class VMWareServerList
    {
        [Key]
        public int Id { get; set; }

        [StringLength(255)]
        [Display(Name = "Service")]
        public string? Service { get; set; }

        [StringLength(255)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [StringLength(255)]
        [Display(Name = "Region")]
        public string? Region { get; set; }

        [StringLength(255)]
        [Display(Name = "vCenter")]
        public string? VCenter { get; set; }

        [StringLength(255)]
        [Display(Name = "Cluster")]
        public string? Cluster { get; set; }

        [StringLength(255)]
        [Display(Name = "VM")]
        public string? VM { get; set; }

        [StringLength(255)]
        [Display(Name = "ESXi")]
        public string? ESXi { get; set; }

        [StringLength(100)]
        [Display(Name = "Status")]
        public string? Status { get; set; }

        [StringLength(500)]
        [Display(Name = "OS")]
        public string? OS { get; set; }

        [StringLength(255)]
        [Display(Name = "DNS Name")]
        public string? DnsName { get; set; }

        [Display(Name = "Total CPU")]
        public int? TotCPU { get; set; }

        [Display(Name = "Socket")]
        public int? Socket { get; set; }

        [Display(Name = "Core")]
        public int? Core { get; set; }

        [Display(Name = "vMem")]
        public long? VMem { get; set; }

        [Display(Name = "Size GB")]
        public decimal? SizeGB { get; set; }

        [StringLength(255)]
        [Display(Name = "Backup Veeam")]
        public string? BackupVeeam { get; set; }

        [StringLength(100)]
        [Display(Name = "vHW")]
        public string? VHW { get; set; }

        [StringLength(100)]
        [Display(Name = "Tools Status")]
        public string? ToolsStatus { get; set; }

        [StringLength(100)]
        [Display(Name = "Tools Version")]
        public string? ToolsVersion { get; set; }

        [StringLength(500)]
        [Display(Name = "IP")]
        public string? IP { get; set; }

        [StringLength(500)]
        [Display(Name = "VMX")]
        public string? VMX { get; set; }

        [StringLength(255)]
        [Display(Name = "Folder")]
        public string? Folder { get; set; }

        [StringLength(255)]
        [Display(Name = "RP")]
        public string? RP { get; set; }

        [StringLength(255)]
        [Display(Name = "SCOPE")]
        public string? SCOPE { get; set; }

        [StringLength(255)]
        [Display(Name = "SUB_SCOPE")]
        public string? SUB_SCOPE { get; set; }

        [StringLength(255)]
        [Display(Name = "CUSTOMER")]
        public string? CUSTOMER { get; set; }

        [StringLength(255)]
        [Display(Name = "Owner")]
        public string? Owner { get; set; }

        [StringLength(255)]
        [Display(Name = "Group")]
        public string? Group { get; set; }

        [StringLength(255)]
        [Display(Name = "Service Owner")]
        public string? ServiceOwner { get; set; }

        [StringLength(255)]
        [Display(Name = "Service Group")]
        public string? ServiceGroup { get; set; }

        [StringLength(255)]
        [Display(Name = "Project")]
        public string? Project { get; set; }

        [Display(Name = "Creation Date")]
        public DateTime? CreationDate { get; set; }

        [Display(Name = "VM Creation Date")]
        public DateTime? VMCreationDate { get; set; }

        [StringLength(255)]
        [Display(Name = "Functionality")]
        public string? Functionality { get; set; }

        [StringLength(255)]
        [Display(Name = "Deploy From")]
        public string? DeployFrom { get; set; }

        [StringLength(255)]
        [Display(Name = "IDEL")]
        public string? IDEL { get; set; }

        [StringLength(255)]
        [Display(Name = "Environment")]
        public string? Environment { get; set; }

        [StringLength(100)]
        [Display(Name = "Priority")]
        public string? Priority { get; set; }

        [StringLength(1000)]
        [Display(Name = "Note")]
        public string? Note { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}