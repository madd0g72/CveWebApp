using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Represents a service that can be installed on servers
    /// </summary>
    public class Service
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Service Name")]
        public string ServiceName { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [StringLength(100)]
        [Display(Name = "Version")]
        public string? Version { get; set; }

        [StringLength(255)]
        [Display(Name = "Vendor")]
        public string? Vendor { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<ServerService> ServerServices { get; set; } = new List<ServerService>();
    }
}