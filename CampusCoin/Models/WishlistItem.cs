namespace CampusCoin.Models
{
    public class WishlistItem
    {
        public int WishlistId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal EstimatedPrice { get; set; }
        public string Priority { get; set; } = "Medium"; // Low, Medium, High
        public bool IsPurchased { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
