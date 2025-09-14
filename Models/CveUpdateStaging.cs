using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CveWebApp.Models
{
    [Table("Staging")]
    public class CveUpdateStaging
    {
        [Key]
        public int Id { get; set; }

        public DateTime? ReleaseDate { get; set; }

        [StringLength(255)]
        public string? ProductFamily { get; set; }

        [StringLength(255)]
        public string? Product { get; set; }

        [StringLength(255)]
        public string? Platform { get; set; }

        [StringLength(255)]
        public string? Impact { get; set; }

        [StringLength(100)]
        public string? MaxSeverity { get; set; }

        [StringLength(255)]
        public string? Article { get; set; }

        [StringLength(500)]
        public string? ArticleLink { get; set; }

        [StringLength(255)]
        public string? Supercedence { get; set; }

        [StringLength(255)]
        public string? Download { get; set; }

        [StringLength(500)]
        public string? DownloadLink { get; set; }

        [StringLength(100)]
        public string? BuildNumber { get; set; }

        [StringLength(1000)]
        public string? Details { get; set; }

        [StringLength(500)]
        public string? DetailsLink { get; set; }

        public decimal? BaseScore { get; set; }

        public decimal? TemporalScore { get; set; }

        public bool? CustomerActionRequired { get; set; }
    }
}