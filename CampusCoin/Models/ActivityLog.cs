namespace CampusCoin.Models
{
    public class ActivityLog
    {
        public int LogId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? TransactionId { get; set; }
        public Transaction? Transaction { get; set; }
        public string ActionType { get; set; } = string.Empty; // Viewed, Edited, FlaggedDuplicate, FlaggedLarge
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
