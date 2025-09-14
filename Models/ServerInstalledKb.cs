using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    public class ServerInstalledKb
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "Computer")]
        public string Computer { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Display(Name = "OS Product")]
        public string OSProduct { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "KB")]
        public string KB { get; set; } = string.Empty;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}