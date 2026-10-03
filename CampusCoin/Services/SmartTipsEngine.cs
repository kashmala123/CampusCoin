using CampusCoin.Data;
using CampusCoin.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CampusCoin.Services
{
    public class SmartTipsEngine
    {
        
        /// <summary>Stable id from title so dismiss/pin survives regeneration.</summary>
        private static int StableTipId(string title)
        {
            if (string.IsNullOrEmpty(title)) return 0;
            unchecked
            {
                int hash = 23;
                foreach (char ch in title.Trim().ToLowerInvariant())
                    hash = hash * 31 + ch;
                return hash == 0 ? 1 : Math.Abs(hash);
            }
        }

        public async Task<List<SmartTipDto>> GenerateTipsAsync(int userId, ApplicationDbContext context, DateTime month)
        {
            var startOfMonth = new DateTime(month.Year, month.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var transactions = await context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= startOfMonth && t.Date < endOfMonth)
                .ToListAsync();

            var user = await context.Users.FindAsync(userId);
            decimal totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpenses = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            var tips = new List<SmartTipDto>();
            // tip ids are stable hashes of title

            // 1. Check Category Spending Ratios (e.g. Food)
            var foodExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Category != null && t.Category.Name == "Food")
                .Sum(t => t.Amount);

            if (totalExpenses > 0 && foodExpenses > 0)
            {
                decimal foodRatio = (foodExpenses / totalExpenses) * 100m;
                if (foodRatio >= 28m)
                {
                    tips.Add(new SmartTipDto
                    {
                        TipId = StableTipId("Food Budget Alert"),
                        Title = "Food Budget Alert",
                        Text = $"Dining out took {foodRatio:F0}% of your total spending (${foodExpenses:F2}). Cooking twice more per week saves ~$45/week.",
                        Category = "Food",
                        Icon = "fa-utensils",
                        Color = "sky",
                        EstimatedImpact = 180.00m
                    });
                }
            }

            // 2. Check Budgets
            var budgets = await context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.Month == startOfMonth)
                .ToListAsync();

            foreach (var budget in budgets)
            {
                decimal spent = transactions
                    .Where(t => t.Type == "Expense" && t.CategoryId == budget.CategoryId)
                    .Sum(t => t.Amount);

                if (budget.LimitAmount > 0)
                {
                    decimal percent = (spent / budget.LimitAmount) * 100m;
                    if (percent >= 100m)
                    {
                        var overTitle = $"{budget.Category?.Name ?? "Category"} Over Budget";
                        tips.Add(new SmartTipDto
                        {
                            TipId = StableTipId(overTitle),
                            Title = overTitle,
                            Text = $"You have exceeded your ${budget.LimitAmount:F2} limit by ${(spent - budget.LimitAmount):F2}. Consider pausing non-essential purchases.",
                            Category = budget.Category?.Name ?? "Budget",
                            Icon = "fa-triangle-exclamation",
                            Color = "rose",
                            EstimatedImpact = spent - budget.LimitAmount
                        });
                    }
                    else if (percent >= 80m)
                    {
                        var nearTitle = $"{budget.Category?.Name ?? "Category"} Approaching Limit";
                        tips.Add(new SmartTipDto
                        {
                            TipId = StableTipId(nearTitle),
                            Title = nearTitle,
                            Text = $"You have utilized {percent:F0}% of your ${budget.LimitAmount:F2} limit with ${(budget.LimitAmount - spent):F2} remaining for this month.",
                            Category = budget.Category?.Name ?? "Budget",
                            Icon = "fa-circle-exclamation",
                            Color = "amber",
                            EstimatedImpact = budget.LimitAmount - spent
                        });
                    }
                }
            }

            // 3. Digital Subscriptions Audit
            var subExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Category != null && t.Category.Name == "Subscriptions")
                .Sum(t => t.Amount);

            if (subExpenses >= 20.00m)
            {
                tips.Add(new SmartTipDto
                {
                    TipId = StableTipId("Subtle Subscription Alert"),
                    Title = "Subtle Subscription Alert",
                    Text = $"You have spent ${subExpenses:F2} on digital subscriptions this month. Review and cancel any unused streaming or gaming apps.",
                    Category = "Subscriptions",
                    Icon = "fa-ticket",
                    Color = "purple",
                    EstimatedImpact = subExpenses * 0.5m
                });
            }

            // 4. Savings Goal Progress
            var activeGoal = await context.SavingsGoals
                .Where(g => g.UserId == userId && !g.IsAchieved)
                .OrderByDescending(g => g.CurrentAmount / (g.TargetAmount > 0 ? g.TargetAmount : 1))
                .FirstOrDefaultAsync();

            if (activeGoal != null && activeGoal.TargetAmount > 0)
            {
                decimal diff = activeGoal.TargetAmount - activeGoal.CurrentAmount;
                if (diff > 0)
                {
                    var goalTitle = $"{activeGoal.GoalName} Target";
                    tips.Add(new SmartTipDto
                    {
                        TipId = StableTipId(goalTitle),
                        Title = goalTitle,
                        Text = $"You are only ${diff:F2} away from reaching your ${activeGoal.TargetAmount:F2} goal buffer!",
                        Category = "Savings",
                        Icon = "fa-piggy-bank",
                        Color = "emerald",
                        EstimatedImpact = diff
                    });
                }
            }

            // 5. General Surplus / Positive reinforcement
            decimal netSavings = totalIncome - totalExpenses;
            if (netSavings > 0)
            {
                tips.Add(new SmartTipDto
                {
                    TipId = StableTipId("Weekend Budget Safety"),
                    Title = "Weekend Budget Safety",
                    Text = $"You are on track to end the month with a ~${netSavings:F2} surplus if discretionary weekend spending stays contained.",
                    Category = "General",
                    Icon = "fa-shield-halved",
                    Color = "emerald",
                    EstimatedImpact = netSavings
                });
            }

            // 6. Textbooks / Academics tip
            var acadExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Category != null && t.Category.Name == "Academics")
                .Sum(t => t.Amount);

            if (acadExpenses > 0)
            {
                tips.Add(new SmartTipDto
                {
                    TipId = StableTipId("Course Material Savings"),
                    Title = "Course Material Savings",
                    Text = "Rent or buy digital textbooks for upcoming courses early to save up to 35% on campus supplies.",
                    Category = "Academics",
                    Icon = "fa-book",
                    Color = "sky",
                    EstimatedImpact = 35.00m
                });
            }

            // 7. Fallback if fewer than 3 tips
            if (tips.Count < 3)
            {
                tips.Add(new SmartTipDto
                {
                    TipId = StableTipId("Campus Commute Tip"),
                    Title = "Campus Commute Tip",
                    Text = "Carpooling with classmates or getting a student transit pass can reduce travel costs by 25%.",
                    Category = "Transport",
                    Icon = "fa-bus",
                    Color = "sky",
                    EstimatedImpact = 30.00m
                });
            }

            // Query user interactions (dismissed or pinned)
            var interactions = await context.TipInteractions
                .Where(i => i.UserId == userId)
                .ToListAsync();

            var dismissedIds = interactions.Where(i => i.IsDismissed).Select(i => i.TipId).ToHashSet();
            var pinnedIds = interactions.Where(i => i.IsPinned).Select(i => i.TipId).ToHashSet();

            tips = tips.Where(t => !dismissedIds.Contains(t.TipId)).ToList();
            foreach (var tip in tips)
            {
                if (pinnedIds.Contains(tip.TipId))
                {
                    tip.IsPinned = true;
                }
            }

            // Sort pinned first, then by estimated impact descending
            return tips
                .OrderByDescending(t => t.IsPinned)
                .ThenByDescending(t => t.EstimatedImpact ?? 0)
                .Take(4)
                .ToList();
        }
    }
}
