using System.ComponentModel.DataAnnotations;

namespace CampusCoin.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string Type { get; set; } = string.Empty; // "Income" or "Expense"

        public bool IsDefault { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    }
}
