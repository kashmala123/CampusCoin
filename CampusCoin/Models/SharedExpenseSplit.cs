namespace CampusCoin.Models
{
    public class SharedExpenseSplit
    {
        public int SplitId { get; set; }
        public int SharedExpenseId { get; set; }
        public SharedExpense? SharedExpense { get; set; }
        public string ParticipantName { get; set; } = string.Empty;
        public decimal ShareAmount { get; set; }
        public bool IsSettled { get; set; }
    }
}
