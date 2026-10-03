using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoin.Models
{
    public class FeeVoucher
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [MaxLength(30)]
        public string VoucherCode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Semester { get; set; } = "Semester 4";

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Fine { get; set; } = 0m;

        // 👇 YEH NAYA FIELD HAI — Admin Voucher Management ke liye
        [Column(TypeName = "decimal(10,2)")]
        public decimal Discount { get; set; } = 0m;

        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime? PaidAt { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? TransactionRef { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}