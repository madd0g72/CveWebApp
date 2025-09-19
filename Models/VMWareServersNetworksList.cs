using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Represents a VMWare server network interface with network configuration information
    /// </summary>
    public class VMWareServersNetworksList
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
        [Display(Name = "VM Name")]
        public string? VmName { get; set; }

        [StringLength(500)]
        [Display(Name = "OS")]
        public string? OS { get; set; }

        [StringLength(100)]
        [Display(Name = "Status")]
        public string? Status { get; set; }

        [StringLength(255)]
        [Display(Name = "Owner")]
        public string? Owner { get; set; }

        [StringLength(100)]
        [Display(Name = "Tools")]
        public string? Tools { get; set; }

        [StringLength(17)]
        [Display(Name = "MAC Address")]
        public string? MacAddress { get; set; }

        [StringLength(45)]
        [Display(Name = "IP Address")]
        public string? IpAddress { get; set; }

        [Display(Name = "Connected")]
        public bool? Connected { get; set; }

        [StringLength(500)]
        [Display(Name = "Port Group")]
        public string? PortGroup { get; set; }

        [StringLength(100)]
        [Display(Name = "Type")]
        public string? Type { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}