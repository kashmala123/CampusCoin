namespace CampusCoin.Models
{
    public class TransactionHistory
    {
        public int HistoryId { get; set; }
        public int TransactionId { get; set; } // not FK-enforced, row may be gone
        public int UserId { get; set; }
        public User? User { get; set; }
        public string ChangeType { get; set; } = string.Empty; // Edited, Deleted
        public decimal? PreviousAmount { get; set; }
        public int? PreviousCategoryId { get; set; }
        public string? PreviousType { get; set; }
        public string? PreviousDescription { get; set; }
        public DateTime? PreviousDate { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
