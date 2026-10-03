namespace CampusCoin.Models
{
    public class UserPreference
    {
        public int UserId { get; set; } // PK + FK (1:1 with Users)
        public User? User { get; set; }
        public bool DarkMode { get; set; }
        public string FontSize { get; set; } = "Medium"; // Small, Medium, Large
        public string Currency { get; set; } = "PKR";
        public DateTime UpdatedAt { get; set; }
    }
}
