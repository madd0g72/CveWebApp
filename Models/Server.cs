using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Represents a server asset with infrastructure and operational information
    /// </summary>
    public class Server
    {
        [Key]
        public int Id { get; set; }

        [StringLength(255)]
        [Display(Name = "VCenter")]
        public string? VCenter { get; set; }

        [StringLength(255)]
        [Display(Name = "Cluster")]
        public string? Cluster { get; set; }

        [StringLength(255)]
        [Display(Name = "Project")]
        public string? Project { get; set; }

        [StringLength(255)]
        [Display(Name = "Environment")]
        public string? Environment { get; set; }

        [StringLength(255)]
        [Display(Name = "IDEL")]
        public string? IDEL { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Server Name")]
        public string ServerName { get; set; } = string.Empty;

        [StringLength(45)]
        [Display(Name = "Server IP")]
        public string? ServerIP { get; set; }

        [StringLength(45)]
        [Display(Name = "Management IP")]
        public string? ManagementIP { get; set; }

        [StringLength(255)]
        [Display(Name = "Operating System")]
        public string? OperatingSystem { get; set; }

        [StringLength(255)]
        [Display(Name = "OS Version")]
        public string? OperatingSystemVersion { get; set; }

        [Display(Name = "Last Boot Time")]
        public DateTime? LastBootTime { get; set; }

        [StringLength(1000)]
        [Display(Name = "Local Admins")]
        public string? LocalAdmins { get; set; }

        [Display(Name = "OS Disk Size (GB)")]
        public decimal? OSDiskSize { get; set; }

        [Display(Name = "OS Disk Free (GB)")]
        public decimal? OSDiskFree { get; set; }

        [StringLength(255)]
        [Display(Name = "Service Owner")]
        public string? ServiceOwner { get; set; }

        [StringLength(500)]
        [Display(Name = "Maintenance Windows")]
        public string? MaintenanceWindows { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<ServerService> ServerServices { get; set; } = new List<ServerService>();

        // Computed properties for related data
        /// <summary>
        /// Gets installed KBs for this server from ServerInstalledKb table
        /// </summary>
        public virtual ICollection<ServerInstalledKb> InstalledKbs { get; set; } = new List<ServerInstalledKb>();
    }
}