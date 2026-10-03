using System.ComponentModel.DataAnnotations.Schema;

namespace CampusCoin.Models
{
    public class Budget
    {
        public int BudgetId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Column("Month")]
        public DateTime Month { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal LimitAmount { get; set; }
    }
}
