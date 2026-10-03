namespace CampusCoin.Models
{
    public class RecurringTransaction
    {
        public int RecurringId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // Income, Expense
        public string? Description { get; set; }
        public string Frequency { get; set; } = "Monthly"; // Weekly, Monthly, Yearly
        public DateTime NextRunDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
