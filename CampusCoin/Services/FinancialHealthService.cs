using CampusCoin.Data;
using CampusCoin.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CampusCoin.Services
{
    public class FinancialHealthService
    {
        public async Task<(int OverallScore, string Status, HealthBreakdownDto Breakdown)> CalculateHealthScoreAsync(int userId, ApplicationDbContext context, DateTime month)
        {
            var startOfMonth = new DateTime(month.Year, month.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var user = await context.Users.FindAsync(userId);
            var allowanceBaseline = user?.MonthlyAllowanceBaseline ?? 1250.00m;
            var monthlyGoal = user?.MonthlySavingsGoal ?? 300.00m;

            // Current month transactions
            var transactions = await context.Transactions
                .Where(t => t.UserId == userId && t.Date >= startOfMonth && t.Date < endOfMonth)
                .ToListAsync();

            decimal totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            if (totalIncome == 0 && allowanceBaseline > 0)
            {
                totalIncome = allowanceBaseline;
            }

            // 1. Budget Control Score (0 - 25)
            int budgetControl = 22;
            var budgets = await context.Budgets
                .Where(b => b.UserId == userId && b.Month == startOfMonth)
                .ToListAsync();

            if (budgets.Count != 0)
            {
                decimal totalBudgetLimit = budgets.Sum(b => b.LimitAmount);
                var categoryIds = budgets.Select(b => b.CategoryId).ToList();
                decimal totalBudgetedSpent = transactions
                    .Where(t => t.Type == "Expense" && categoryIds.Contains(t.CategoryId))
                    .Sum(t => t.Amount);

                if (totalBudgetLimit > 0)
                {
                    decimal ratio = totalBudgetedSpent / totalBudgetLimit;
                    if (ratio <= 0.80m) budgetControl = 25;
                    else if (ratio <= 1.00m) budgetControl = (int)(25 - ((ratio - 0.80m) * 25));
                    else if (ratio <= 1.20m) budgetControl = Math.Max(8, (int)(20 - ((ratio - 1.00m) * 50)));
                    else budgetControl = 5;
                }
            }
            else if (allowanceBaseline > 0)
            {
                decimal ratio = totalExpense / allowanceBaseline;
                if (ratio <= 0.70m) budgetControl = 24;
                else if (ratio <= 0.90m) budgetControl = 20;
                else if (ratio <= 1.00m) budgetControl = 16;
                else budgetControl = 8;
            }

            // 2. Saving Rate Score (0 - 25)
            int savingRate = 20;
            if (totalIncome > 0)
            {
                decimal netSavings = totalIncome - totalExpense;
                decimal ratePercent = (netSavings / totalIncome) * 100m;

                if (ratePercent >= 25m) savingRate = 25;
                else if (ratePercent >= 15m) savingRate = 21;
                else if (ratePercent >= 5m) savingRate = 16;
                else if (ratePercent >= 0m) savingRate = 11;
                else savingRate = Math.Max(2, (int)(8 - (Math.Abs(ratePercent) / 10m)));
            }

            // 3. Goal Progress Score (0 - 25)
            int goalProgress = 20;
            var activeGoals = await context.SavingsGoals
                .Where(g => g.UserId == userId && !g.IsAchieved)
                .ToListAsync();

            if (activeGoals.Count != 0)
            {
                decimal totalTarget = activeGoals.Sum(g => g.TargetAmount);
                decimal totalCurrent = activeGoals.Sum(g => g.CurrentAmount);
                if (totalTarget > 0)
                {
                    decimal progressPercent = (totalCurrent / totalTarget) * 100m;
                    goalProgress = Math.Min(25, Math.Max(5, (int)(progressPercent * 0.25m)));
                }
            }
            else if (monthlyGoal > 0)
            {
                decimal netSavings = Math.Max(0, totalIncome - totalExpense);
                decimal progress = (netSavings / monthlyGoal) * 100m;
                goalProgress = Math.Min(25, Math.Max(8, (int)(progress * 0.25m)));
            }

            // 4. Spending Stability Score (0 - 25)
            int stability = 21;
            var pastStart = startOfMonth.AddMonths(-3);
            var pastExpenses = await context.Transactions
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.Date >= pastStart && t.Date < startOfMonth)
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => g.Sum(x => x.Amount))
                .ToListAsync();

            if (pastExpenses.Count != 0)
            {
                decimal avgPast = pastExpenses.Average();
                if (avgPast > 0)
                {
                    decimal variance = Math.Abs(totalExpense - avgPast) / avgPast;
                    if (variance <= 0.15m) stability = 25;
                    else if (variance <= 0.30m) stability = 20;
                    else if (variance <= 0.50m) stability = 15;
                    else stability = 8;
                }
            }

            int overall = budgetControl + savingRate + goalProgress + stability;
            overall = Math.Clamp(overall, 15, 100);

            string status = overall switch
            {
                >= 80 => "Excellent Health",
                >= 65 => "Good Health",
                >= 50 => "Fair Health",
                _ => "Needs Attention"
            };

            var breakdown = new HealthBreakdownDto
            {
                BudgetControlScore = budgetControl,
                SavingRateScore = savingRate,
                GoalProgressScore = goalProgress,
                ExpenseStabilityScore = stability
            };

            return (overall, status, breakdown);
        }
    }
}
