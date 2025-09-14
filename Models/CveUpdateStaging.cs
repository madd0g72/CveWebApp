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
        public string? ProductFamily { get; set; }

        [Display(Name = "Product")]
        public string? Product { get; set; }

        [Display(Name = "Platform")]
        public string? Platform { get; set; }

        [Display(Name = "Impact")]
        public string? Impact { get; set; }

        [Display(Name = "Max Severity")]
        public string? MaxSeverity { get; set; }

        [Display(Name = "Article")]
        public string? Article { get; set; }

        [Display(Name = "Article Link")]
        public string? ArticleLink { get; set; }

        [Display(Name = "Supercedence")]
        public string? Supercedence { get; set; }

        [Display(Name = "Download")]
        public string? Download { get; set; }

        [Display(Name = "Download Link")]
        public string? DownloadLink { get; set; }

        [Display(Name = "Build Number")]
        public string? BuildNumber { get; set; }

        [Display(Name = "CVE")]
        public string? Details { get; set; } // This is your CVE identifier

        [Display(Name = "Details Link")]
        public string? DetailsLink { get; set; }

        [Display(Name = "Base Score")]
        public decimal? BaseScore { get; set; } // decimal for scores like 8.3, 5.1

        [Display(Name = "Temporal Score")]
        public decimal? TemporalScore { get; set; } // decimal for scores like 8.3, 5.1

        [Display(Name = "Customer Action Required")]
        public bool? CustomerActionRequired { get; set; }
    }
}