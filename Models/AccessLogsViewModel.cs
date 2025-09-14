using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// View model for access logs display and filtering
    /// </summary>
    public class AccessLogsViewModel
    {
        /// <summary>
        /// List of login attempts
        /// </summary>
        public List<LoginAttempt> LoginAttempts { get; set; } = new List<LoginAttempt>();

        /// <summary>
        /// Filter by username
        /// </summary>
        [Display(Name = "Username")]
        public string? UsernameFilter { get; set; }

        /// <summary>
        /// Filter by IP address
        /// </summary>
        [Display(Name = "IP Address")]
        public string? IpFilter { get; set; }

        /// <summary>
        /// Filter by success/failure
        /// </summary>
        [Display(Name = "Result")]
        public bool? SuccessFilter { get; set; }

        /// <summary>
        /// Filter by date range - start
        /// </summary>
        [Display(Name = "From Date")]
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Filter by date range - end
        /// </summary>
        [Display(Name = "To Date")]
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Current page number for pagination
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; } = 25;

        /// <summary>
        /// Total number of items
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

        /// <summary>
        /// Helper property to check if there are more pages
        /// </summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>
        /// Helper property to check if there are more pages
        /// </summary>
        public bool HasNextPage => Page < TotalPages;
    }
}