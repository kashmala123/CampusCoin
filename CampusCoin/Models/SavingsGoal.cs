namespace CampusCoin.Models
{
    public class SavingsGoal
    {
        public int GoalId { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string GoalName { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public DateTime? TargetDate { get; set; }
        public bool IsAchieved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
