using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Represents KB supersedence relationships where newer KBs replace older ones
    /// </summary>
    public class KbSupersedence
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Original KB")]
        public string OriginalKb { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        [Display(Name = "Superseding KB")]
        public string SupersedingKb { get; set; } = string.Empty;

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        [Display(Name = "Product")]
        public string? Product { get; set; }

        [StringLength(255)]
        [Display(Name = "Product Family")]
        public string? ProductFamily { get; set; }
    }
}