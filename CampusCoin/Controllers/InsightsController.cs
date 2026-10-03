using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    [ApiController]
    [Route("api/[controller]")]
    public class InsightsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InsightsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/insights/{month} =================
        [HttpGet("{month}")]
        public async Task<IActionResult> GetMonthlyInsight(string month)
        {
            int userId = GetCurrentUserId();

            if (!DateTime.TryParse(month + "-01", out var monthDate))
            {
                monthDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            }

            var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var existing = await _context.Insights
                .FirstOrDefaultAsync(i => i.UserId == userId && i.Month == startOfMonth);

            if (existing != null)
            {
                return Ok(existing);
            }

            // Generate Insight from real data
            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= startOfMonth && t.Date < endOfMonth)
                .ToListAsync();

            decimal income = txns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal expense = txns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            var topCat = txns.Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category?.Name ?? "General")
                .OrderByDescending(g => g.Sum(x => x.Amount))
                .FirstOrDefault();

            string topCatName = topCat?.Key ?? "General";
            decimal topCatAmount = topCat?.Sum(x => x.Amount) ?? 0m;

            string summary = $"In {startOfMonth:MMMM yyyy}, you logged ${income:F2} in income and ${expense:F2} in total expenses. Your highest expense category was {topCatName} at ${topCatAmount:F2}.";
            string tip = expense > income 
                ? "Your expenses exceeded your income this month. Prioritize curbing discretionary spending next month."
                : $"Great job saving ${(income - expense):F2}! Consider depositing part of this surplus into your emergency buffer.";

            var newInsight = new Insight
            {
                UserId = userId,
                Month = startOfMonth,
                SummaryText = summary,
                TipText = tip,
                GeneratedAt = DateTime.UtcNow
            };

            _context.Insights.Add(newInsight);
            await _context.SaveChangesAsync();

            return Ok(newInsight);
        }
    }
}
