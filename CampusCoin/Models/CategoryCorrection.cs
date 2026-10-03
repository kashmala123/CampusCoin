namespace CampusCoin.Models
{
    public class CategoryCorrection
    {
        public int CorrectionId { get; set; }
        public int TransactionId { get; set; }
        public Transaction? Transaction { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? SuggestedCategoryId { get; set; }
        public Category? SuggestedCategory { get; set; }
        public int ActualCategoryId { get; set; }
        public Category? ActualCategory { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
