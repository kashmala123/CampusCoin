using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoin.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(30)]
        public string EventCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = "Workshop";

        [MaxLength(20)]
        public string Status { get; set; } = "Upcoming";

        /// <summary>Public image URL only (not base64). Longer values are rejected/replaced.</summary>
        [MaxLength(1000)]
        public string? Image { get; set; }

        public DateTime EventDate { get; set; }

        [MaxLength(100)]
        public string? Time { get; set; }

        [MaxLength(200)]
        public string? Venue { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Fee { get; set; } = 0;

        public int SeatsTotal { get; set; } = 100;

        public int SeatsLeft { get; set; } = 100;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public bool AutoApprove { get; set; } = true;

        public bool WalletIntegration { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
