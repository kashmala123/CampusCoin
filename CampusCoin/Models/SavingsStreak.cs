namespace CampusCoin.Models
{
    public class SavingsStreak
    {
        public int UserId { get; set; } // PK + FK (1:1 with Users)
        public User? User { get; set; }
        public int CurrentStreakMonths { get; set; }
        public int LongestStreakMonths { get; set; }
        public DateTime? LastQualifyingMonth { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
