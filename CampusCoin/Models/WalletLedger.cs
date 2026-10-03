using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoin.Models
{
    /// <summary>
    /// Dedicated wallet movement log (top-up, transfer in/out, fee pay, event pay).
    /// Balance itself lives on User.MonthlyAllowanceBaseline.
    /// </summary>
    [Table("WalletLedgers")]
    public class WalletLedger
    {
        [Key]
        public int LedgerId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>TopUp, TransferIn, TransferOut, FeePay, EventPay, Adjustment</summary>
        [Required, MaxLength(30)]
        public string EntryType { get; set; } = string.Empty;

        /// <summary>Credit increases balance; Debit decreases.</summary>
        [Required, MaxLength(10)]
        public string Direction { get; set; } = "Credit";

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal BalanceAfter { get; set; }

        [MaxLength(40)]
        public string? Method { get; set; }

        [MaxLength(40)]
        public string? CounterpartyAccount { get; set; }

        [MaxLength(150)]
        public string? CounterpartyName { get; set; }

        [MaxLength(250)]
        public string? Description { get; set; }

        [MaxLength(80)]
        public string? ReferenceCode { get; set; }

        public int? RelatedUserId { get; set; }
        public int? RelatedTransactionId { get; set; }
        public int? RelatedFeeVoucherId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
