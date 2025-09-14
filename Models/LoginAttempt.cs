using System.ComponentModel.DataAnnotations;

namespace CveWebApp.Models
{
    /// <summary>
    /// Model for tracking login attempts and application access logs
    /// </summary>
    public class LoginAttempt
    {
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(256)]
        public string Username { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(45)]
        public string SourceIP { get; set; } = string.Empty;

        [Required]
        public bool IsSuccess { get; set; }

        [MaxLength(500)]
        public string? FailureReason { get; set; }

        [MaxLength(256)]
        public string? UserAgent { get; set; }
    }
}