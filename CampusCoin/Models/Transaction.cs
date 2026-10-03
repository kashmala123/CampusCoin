using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoin.Models
{
    [Table("Transactions")]
    public class Transaction
    {
        public int TransactionId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required, MaxLength(10)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Description { get; set; }

        public int? AiSuggestedCategoryId { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? AiConfidenceScore { get; set; }

        [Column("Date")]
        public DateTime Date { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsRecurring { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public bool IsFlagged { get; set; } = false;
    }
}