using System.ComponentModel.DataAnnotations;

namespace CampusCoin.Models
{
    public class User
    {
        public int UserId { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? AcademicYear { get; set; }

        public decimal MonthlySavingsGoal { get; set; }

        public decimal MonthlyAllowanceBaseline { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Optional wallet transfer PIN (Identity password-hash of the 4-digit PIN).</summary>
        [MaxLength(256)]
        public string? WalletPin { get; set; }

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public DateTime CreatedAt { get; set; }

        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    }
}
