using System.ComponentModel.DataAnnotations;

namespace CampusCoin.Models.DTOs
{
    public class TransactionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense"; // Expense or Income
        public bool IsRecurring { get; set; }

        // Smart Spending Alerts (optional — only set on create when budget threshold hit)
        public string? BudgetAlertMessage { get; set; }
        public string? BudgetAlertSeverity { get; set; } // "over" | "warning" | null
        public decimal? BudgetAlertPercent { get; set; }
        public decimal? BudgetRemaining { get; set; }
    }

    public class CreateTransactionDto
    {
        [Required, MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; } = "Expense"; // Expense or Income

        [Required]
        public string Category { get; set; } = string.Empty; // Category name or ID

        public string? Date { get; set; } // YYYY-MM-DD (defaults to today)

        public bool IsRecurring { get; set; } = false;

        public string RecurringFrequency { get; set; } = "Monthly"; // Weekly, Monthly, Yearly

        /// <summary>Category suggested by AI/rules before user confirmed (for learning).</summary>
        public int? SuggestedCategoryId { get; set; }
    }

    public class UpdateTransactionDto
    {
        [Required]
        public int Id { get; set; }

        [Required, MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; } = "Expense";

        [Required]
        public string Category { get; set; } = string.Empty;

        public string? Date { get; set; }
    }

    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense";
        public bool IsDefault { get; set; }
        public bool IsUserCustom { get; set; }
    }

    public class CreateCategoryDto
    {
        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Type { get; set; } = "Expense"; // Income or Expense
    }

    public class BudgetDto
    {
        public int BudgetId { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty; // YYYY-MM
        public decimal LimitAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal PercentageUsed { get; set; }
        public string Status { get; set; } = "Safe"; // Safe, Warning, Exceeded
    }

    public class SetBudgetDto
    {
        [Required]
        public int CategoryId { get; set; }

        public string? Month { get; set; } // YYYY-MM

        [Required, Range(1, 1000000)]
        public decimal LimitAmount { get; set; }
    }

    public class SavingsGoalDto
    {
        public int GoalId { get; set; }
        public string GoalName { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; }
        public decimal ProgressPercentage { get; set; }
        public string? TargetDate { get; set; }
        public string? EstimatedCompletionDate { get; set; }
        public bool IsAchieved { get; set; }
    }

    public class CreateSavingsGoalDto
    {
        [Required, MaxLength(100)]
        public string GoalName { get; set; } = string.Empty;

        [Required, Range(1, 10000000)]
        public decimal TargetAmount { get; set; }

        public decimal CurrentAmount { get; set; } = 0;

        public DateTime? TargetDate { get; set; }
    }

    public class ContributeGoalDto
    {
        [Required, Range(0.01, 1000000)]
        public decimal Amount { get; set; }
    }
}
