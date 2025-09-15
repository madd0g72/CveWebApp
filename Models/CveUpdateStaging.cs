using System;
using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    public class CveUpdateStaging
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Release Date")]
        public DateTime? ReleaseDate { get; set; }

        [Display(Name = "Product Family")]
        [StringLength(255)]
        public string? ProductFamily { get; set; }

        [Display(Name = "Product")]
        [StringLength(255)]
        public string? Product { get; set; }

        [Display(Name = "Platform")]
        [StringLength(255)]
        public string? Platform { get; set; }

        [Display(Name = "Impact")]
        [StringLength(255)]
        public string? Impact { get; set; }

        [Display(Name = "Max Severity")]
        [StringLength(100)]
        public string? MaxSeverity { get; set; }

        [Display(Name = "Article")]
        [StringLength(500)]
        public string? Article { get; set; }

        [Display(Name = "Article Link")]
        [StringLength(500)]
        public string? ArticleLink { get; set; }

        [Display(Name = "Supercedence")]
        [StringLength(255)]
        public string? Supercedence { get; set; }

        [Display(Name = "Download")]
        [StringLength(255)]
        public string? Download { get; set; }

        [Display(Name = "Download Link")]
        [StringLength(500)]
        public string? DownloadLink { get; set; }

        [Display(Name = "Build Number")]
        [StringLength(100)]
        public string? BuildNumber { get; set; }

        [Display(Name = "CVE")]
        [StringLength(1000)]
        public string? Details { get; set; } // This is your CVE identifier

        [Display(Name = "Details Link")]
        [StringLength(500)]
        public string? DetailsLink { get; set; }

        [Display(Name = "Base Score")]
        public decimal? BaseScore { get; set; } // decimal for scores like 8.3, 5.1

        [Display(Name = "Temporal Score")]
        public decimal? TemporalScore { get; set; } // decimal for scores like 8.3, 5.1

        [Display(Name = "Customer Action Required")]
        public bool? CustomerActionRequired { get; set; }
    }
}