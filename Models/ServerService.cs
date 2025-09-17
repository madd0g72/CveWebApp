using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Represents the many-to-many relationship between servers and services
    /// </summary>
    public class ServerService
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServerId { get; set; }

        [Required]
        public int ServiceId { get; set; }

        [StringLength(100)]
        [Display(Name = "Installed Version")]
        public string? InstalledVersion { get; set; }

        [Display(Name = "Installation Date")]
        public DateTime? InstallationDate { get; set; }

        [Display(Name = "Is Running")]
        public bool IsRunning { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "Configuration Notes")]
        public string? ConfigurationNotes { get; set; }

        [Display(Name = "Last Checked")]
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Server Server { get; set; } = null!;
        public virtual Service Service { get; set; } = null!;
    }
}