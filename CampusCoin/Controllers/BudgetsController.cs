using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class BudgetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BudgetsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/budgets =================
        [HttpGet]
        public async Task<IActionResult> GetBudgets([FromQuery] string? month = null)
        {
            int userId = GetCurrentUserId();

            DateTime targetMonth;
            if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParse(month + "-01", out var parsed))
            {
                targetMonth = new DateTime(parsed.Year, parsed.Month, 1);
            }
            else
            {
                var now = DateTime.UtcNow;
                targetMonth = new DateTime(now.Year, now.Month, 1);
            }

            var nextMonth = targetMonth.AddMonths(1);

            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.Month == targetMonth)
                .ToListAsync();

            // Calculate spent amount for each budget category
            var spentByCat = await _context.Transactions
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.Date >= targetMonth && t.Date < nextMonth)
                .GroupBy(t => t.CategoryId)
                .Select(g => new { CategoryId = g.Key, TotalSpent = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.CategoryId, x => x.TotalSpent);

            var result = budgets.Select(b =>
            {
                decimal spent = spentByCat.GetValueOrDefault(b.CategoryId, 0m);
                decimal remaining = b.LimitAmount - spent;
                decimal percent = b.LimitAmount > 0 ? (spent / b.LimitAmount) * 100m : 0m;
                string status = percent >= 100m ? "Exceeded" : percent >= 80m ? "Warning" : "Safe";

                return new BudgetDto
                {
                    BudgetId = b.BudgetId,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category?.Name ?? "Category",
                    Month = b.Month.ToString("yyyy-MM"),
                    LimitAmount = b.LimitAmount,
                    SpentAmount = spent,
                    RemainingAmount = remaining,
                    PercentageUsed = Math.Round(percent, 1),
                    Status = status
                };
            }).ToList();

            return Ok(result);
        }

        // ================= POST: /api/budgets =================
        [HttpPost]
        public async Task<IActionResult> SetBudget([FromBody] SetBudgetDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Please provide valid budget details." });
            }

            int userId = GetCurrentUserId();
            DateTime targetMonth;
            if (!string.IsNullOrWhiteSpace(model.Month) && DateTime.TryParse(model.Month + "-01", out var parsed))
            {
                targetMonth = new DateTime(parsed.Year, parsed.Month, 1);
            }
            else
            {
                var now = DateTime.UtcNow;
                targetMonth = new DateTime(now.Year, now.Month, 1);
            }

            var category = await _context.Categories.FindAsync(model.CategoryId);
            if (category == null)
            {
                return NotFound(new { success = false, message = "Category not found." });
            }

            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == model.CategoryId && b.Month == targetMonth);

            if (budget == null)
            {
                budget = new Budget
                {
                    UserId = userId,
                    CategoryId = model.CategoryId,
                    Month = targetMonth,
                    LimitAmount = Math.Abs(model.LimitAmount)
                };
                _context.Budgets.Add(budget);
            }
            else
            {
                budget.LimitAmount = Math.Abs(model.LimitAmount);
            }

            await _context.SaveChangesAsync();

            // Calculate spent
            var nextMonth = targetMonth.AddMonths(1);
            decimal spent = await _context.Transactions
                .Where(t => t.UserId == userId && t.CategoryId == model.CategoryId && t.Type == "Expense" && t.Date >= targetMonth && t.Date < nextMonth)
                .SumAsync(t => t.Amount);

            decimal remaining = budget.LimitAmount - spent;
            decimal percent = budget.LimitAmount > 0 ? (spent / budget.LimitAmount) * 100m : 0m;
            string status = percent >= 100m ? "Exceeded" : percent >= 80m ? "Warning" : "Safe";

            return Ok(new BudgetDto
            {
                BudgetId = budget.BudgetId,
                CategoryId = budget.CategoryId,
                CategoryName = category.Name,
                Month = budget.Month.ToString("yyyy-MM"),
                LimitAmount = budget.LimitAmount,
                SpentAmount = spent,
                RemainingAmount = remaining,
                PercentageUsed = Math.Round(percent, 1),
                Status = status
            });
        }

        // ================= DELETE: /api/budgets/{id} =================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            int userId = GetCurrentUserId();
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);

            if (budget == null)
            {
                return NotFound(new { success = false, message = "Budget not found." });
            }

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Budget limit removed." });
        }
    }
}
