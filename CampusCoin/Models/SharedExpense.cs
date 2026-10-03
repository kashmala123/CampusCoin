namespace CampusCoin.Models
{
    public class SharedExpense
    {
        public int SharedExpenseId { get; set; }
        public int TransactionId { get; set; }
        public Transaction? Transaction { get; set; }
        public int PayerUserId { get; set; }
        public User? PayerUser { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
