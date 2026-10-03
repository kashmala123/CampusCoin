using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public StudentController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= VIEWS =================
        [HttpGet("/Student")]
        [HttpGet("/Student/Dashboard")]
        public IActionResult Dashboard() => View();

        [HttpGet("/Student/Transactions")]
        public IActionResult Transactions() => View();

        [HttpGet("/Student/Budgets")]
        public IActionResult Budgets() => View();

        [HttpGet("/Student/SavingsGoals")]
        public IActionResult SavingsGoals() => View();

        [HttpGet("/Student/Reports")]
        public IActionResult Reports() => View();

        // ============================================================
        // ============== DASHBOARD SUMMARY ===========================
        // ============================================================

        [HttpGet("/api/student/summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound();

                var monthTxns = await _context.Transactions
                    .Where(t => t.UserId == userId && t.Date >= monthStart && t.Date < monthEnd)
                    .ToListAsync();

                decimal monthIncome = monthTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal monthExpense = monthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                var allTxns = await _context.Transactions
                    .Where(t => t.UserId == userId)
                    .ToListAsync();

                decimal totalIncome = allTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal totalExpense = allTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                var hs = await _context.FinancialHealthScores
                    .FirstOrDefaultAsync(h => h.UserId == userId);
                int healthScore = hs != null ? Convert.ToInt32(hs.OverallScore) : 75;

                // Top category this month
                var topCat = await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == userId && t.Type == "Expense"
                             && t.Date >= monthStart && t.Date < monthEnd)
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                    .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Amount), Count = g.Count() })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefaultAsync();

                // Saving goals count
                var activeGoals = await _context.SavingsGoals
                    .CountAsync(g => g.UserId == userId && !g.IsAchieved);

                // Budget alerts count (over 85%)
                var budgets = await _context.Budgets
                    .Where(b => b.UserId == userId).ToListAsync();

                int budgetAlerts = 0;
                foreach (var b in budgets)
                {
                    var spent = allTxns.Where(t => t.Type == "Expense" && t.CategoryId == b.CategoryId).Sum(t => t.Amount);
                    if (b.LimitAmount > 0 && (spent / b.LimitAmount) * 100 >= 85) budgetAlerts++;
                }

                // Recent 5 transactions
                var recent = await _context.Transactions
                    .Include(t => t.Category)
                    .Where(t => t.UserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(5)
                    .Select(t => new
                    {
                        id = t.TransactionId,
                        category = t.Category != null ? t.Category.Name : "Other",
                        amount = t.Amount,
                        type = t.Type,
                        date = t.Date.ToString("yyyy-MM-dd")
                    })
                    .ToListAsync();

                return Ok(new
                {
                    user = new
                    {
                        name = user.FullName,
                        email = user.Email,
                        academicYear = user.AcademicYear ?? "N/A",
                        initials = string.Join("", user.FullName.Split(' ').Take(2).Select(n => n.Length > 0 ? n[0].ToString() : "")).ToUpper()
                    },
                    month = new
                    {
                        income = monthIncome,
                        expense = monthExpense,
                        balance = monthIncome - monthExpense,
                        transactionCount = monthTxns.Count
                    },
                    totals = new
                    {
                        income = totalIncome,
                        expense = totalExpense,
                        balance = totalIncome - totalExpense
                    },
                    healthScore,
                    topCategory = topCat != null ? new { name = topCat.Name, amount = topCat.Total, count = topCat.Count } : null,
                    activeGoals,
                    budgetAlerts,
                    recentTransactions = recent
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // ============== CATEGORIES (personal + default) =============
        // ============================================================

        [HttpGet("/api/student/categories")]
        public async Task<IActionResult> GetCategories()
        {
            int userId = GetCurrentUserId();
            var cats = await _context.Categories
                .Where(c => c.IsDefault || c.UserId == userId)
                .OrderBy(c => c.Type).ThenBy(c => c.Name)
                .Select(c => new { c.CategoryId, c.Name, c.Type, c.IsDefault })
                .ToListAsync();
            return Ok(cats);
        }

        [HttpPost("/api/student/categories")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddCategory([FromBody] StudentCategoryRequest model)
        {
            int userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { success = false, message = "Name required." });

            var cat = new Category
            {
                Name = model.Name.Trim(),
                Type = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense",
                IsDefault = false,
                UserId = userId
            };
            _context.Categories.Add(cat);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = cat.CategoryId });
        }

        // ============================================================
        // ============== TRANSACTIONS ================================
        // ============================================================

        [HttpGet("/api/student/transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            int userId = GetCurrentUserId();
            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var result = txns.Select(t => new
            {
                id = t.TransactionId,
                category = t.Category != null ? t.Category.Name : "Other",
                categoryId = t.CategoryId,
                type = t.Type,
                amount = t.Amount,
                date = t.Date.ToString("yyyy-MM-dd"),
                createdAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                note = ""
            }).ToList();

            return Ok(result);
        }

        [HttpPost("/api/student/transactions")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddTransaction([FromBody] StudentTransactionRequest model)
        {
            try
            {
                int userId = GetCurrentUserId();

                if (model.Amount <= 0)
                    return BadRequest(new { success = false, message = "Amount must be greater than 0." });
                if (string.IsNullOrWhiteSpace(model.Type))
                    return BadRequest(new { success = false, message = "Type required." });

                var txn = new Transaction
                {
                    UserId = userId,
                    CategoryId = model.CategoryId,
                    Amount = model.Amount,
                    Type = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense",
                    Date = model.Date != default ? model.Date : DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(txn);
                await _context.SaveChangesAsync();

                // ============ AUTO BUDGET NOTIFICATION (SRS) ============
                if (txn.Type == "Expense")
                {
                    var budget = await _context.Budgets
                        .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == txn.CategoryId);

                    if (budget != null && budget.LimitAmount > 0)
                    {
                        var used = await _context.Transactions
                            .Where(t => t.Type == "Expense" && t.UserId == userId && t.CategoryId == txn.CategoryId)
                            .SumAsync(t => (decimal?)t.Amount) ?? 0;

                        int pct = (int)Math.Round((used / budget.LimitAmount) * 100);
                        var cat = await _context.Categories.FindAsync(txn.CategoryId);
                        string catName = cat?.Name ?? "category";

                        if (pct >= 100)
                        {
                            _context.Notifications.Add(new Notification
                            {
                                UserId = userId,
                                Message = $"🚨 Budget Alert: You've exceeded your {catName} budget by ${(used - budget.LimitAmount):F2} ({pct}% used).",
                                Type = "Warning",
                                IsRead = false,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        else if (pct >= 85)
                        {
                            _context.Notifications.Add(new Notification
                            {
                                UserId = userId,
                                Message = $"⚠️ Budget Alert: You've used {pct}% of your {catName} budget (${used:F2} of ${budget.LimitAmount:F2}).",
                                Type = "Info",
                                IsRead = false,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true, id = txn.TransactionId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        [HttpPut("/api/student/transactions/{id}")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateTransaction(int id, [FromBody] StudentTransactionRequest model)
        {
            int userId = GetCurrentUserId();
            var t = await _context.Transactions.FirstOrDefaultAsync(x => x.TransactionId == id && x.UserId == userId);
            if (t == null) return NotFound();

            t.Amount = model.Amount;
            t.Type = model.Type;
            t.CategoryId = model.CategoryId;
            if (model.Date != default) t.Date = model.Date;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("/api/student/transactions/{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            int userId = GetCurrentUserId();
            var t = await _context.Transactions.FirstOrDefaultAsync(x => x.TransactionId == id && x.UserId == userId);
            if (t == null) return NotFound();
            _context.Transactions.Remove(t);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============================================================
        // ============== BUDGETS =====================================
        // ============================================================

        [HttpGet("/api/student/budgets")]
        public async Task<IActionResult> GetBudgets()
        {
            int userId = GetCurrentUserId();
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId).ToListAsync();

            var allTxns = await _context.Transactions
                .Where(t => t.UserId == userId && t.Type == "Expense").ToListAsync();

            var result = budgets.Select(b =>
            {
                var spent = allTxns.Where(t => t.CategoryId == b.CategoryId).Sum(t => t.Amount);
                int pct = b.LimitAmount > 0 ? (int)Math.Round((spent / b.LimitAmount) * 100) : 0;
                return new
                {
                    id = b.BudgetId,
                    categoryId = b.CategoryId,
                    category = b.Category != null ? b.Category.Name : "Other",
                    limit = b.LimitAmount,
                    spent = spent,
                    percent = pct,
                    status = pct >= 100 ? "Over" : (pct >= 85 ? "Near" : "On Track")
                };
            }).ToList();

            return Ok(result);
        }

        [HttpPost("/api/student/budgets")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateBudget([FromBody] StudentBudgetRequest model)
        {
            int userId = GetCurrentUserId();
            var b = new Budget
            {
                UserId = userId,
                CategoryId = model.CategoryId,
                LimitAmount = model.Limit,
                Month = DateTime.UtcNow
            };
            _context.Budgets.Add(b);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = b.BudgetId });
        }

        [HttpDelete("/api/student/budgets/{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            int userId = GetCurrentUserId();
            var b = await _context.Budgets.FirstOrDefaultAsync(x => x.BudgetId == id && x.UserId == userId);
            if (b == null) return NotFound();
            _context.Budgets.Remove(b);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============================================================
        // ============== SAVING GOALS ================================
        // ============================================================

        [HttpGet("/api/student/saving-goals")]
        public async Task<IActionResult> GetSavingGoals()
        {
            int userId = GetCurrentUserId();
            var goals = await _context.SavingsGoals
                .Where(g => g.UserId == userId).OrderByDescending(g => g.GoalId).ToListAsync();

            var palette = new[] { "#38bdf8", "#10b981", "#6366f1", "#f59e0b", "#ec4899" };
            var icons = new[] { "fa-bullseye", "fa-laptop", "fa-plane", "fa-shield-halved", "fa-gift" };

            var result = goals.Select((g, i) => new
            {
                id = g.GoalId,
                title = g.GoalName,
                target = g.TargetAmount,
                saved = g.CurrentAmount,
                percent = g.TargetAmount > 0 ? Math.Min(100, (int)Math.Round((g.CurrentAmount / g.TargetAmount) * 100)) : 0,
                isAchieved = g.IsAchieved,
                date = g.TargetDate?.ToString("yyyy-MM-dd") ?? "",
                icon = icons[i % icons.Length],
                color = palette[i % palette.Length]
            }).ToList();

            return Ok(result);
        }

        [HttpPost("/api/student/saving-goals")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateSavingGoal([FromBody] StudentGoalRequest model)
        {
            int userId = GetCurrentUserId();
            var g = new SavingsGoal
            {
                UserId = userId,
                GoalName = model.Title.Trim(),
                TargetAmount = model.Target,
                CurrentAmount = model.Saved,
                IsAchieved = model.Saved >= model.Target,
                TargetDate = model.Date != default ? model.Date : DateTime.UtcNow.AddMonths(6)
            };
            _context.SavingsGoals.Add(g);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = g.GoalId });
        }

        [HttpPost("/api/student/saving-goals/{id}/deposit")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DepositGoal(int id, [FromBody] StudentDepositRequest model)
        {
            int userId = GetCurrentUserId();
            var g = await _context.SavingsGoals.FirstOrDefaultAsync(x => x.GoalId == id && x.UserId == userId);
            if (g == null) return NotFound();
            g.CurrentAmount = Math.Min(g.TargetAmount, g.CurrentAmount + model.Amount);
            g.IsAchieved = g.CurrentAmount >= g.TargetAmount;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, current = g.CurrentAmount, isAchieved = g.IsAchieved });
        }

        [HttpDelete("/api/student/saving-goals/{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            int userId = GetCurrentUserId();
            var g = await _context.SavingsGoals.FirstOrDefaultAsync(x => x.GoalId == id && x.UserId == userId);
            if (g == null) return NotFound();
            _context.SavingsGoals.Remove(g);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============================================================
        // ============== NOTIFICATIONS ===============================
        // ============================================================

        [HttpGet("/api/student/notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            int userId = GetCurrentUserId();
            var list = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new
                {
                    id = n.NotificationId,
                    message = n.Message,
                    type = n.Type,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost("/api/student/notifications/{id}/read")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            int userId = GetCurrentUserId();
            var n = await _context.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId);
            if (n == null) return NotFound();
            n.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ============================================================
        // ============== REPORTS =====================================
        // ============================================================

        [HttpGet("/api/student/reports")]
        public async Task<IActionResult> GetReports(
            [FromQuery] string timeframe = "month",
            [FromQuery] string? dateFrom = null,
            [FromQuery] string? dateTo = null,
            [FromQuery] int? categoryId = null)
        {
            try
            {
                int userId = GetCurrentUserId();
                var now = DateTime.UtcNow;
                DateTime fromDate;
                DateTime toDate = now.AddDays(1);

                if (!string.IsNullOrWhiteSpace(dateFrom) && DateTime.TryParse(dateFrom, out var parsedFrom))
                {
                    fromDate = parsedFrom.Date;
                    if (!string.IsNullOrWhiteSpace(dateTo) && DateTime.TryParse(dateTo, out var parsedTo))
                        toDate = parsedTo.Date.AddDays(1);
                }
                else
                {
                    switch ((timeframe ?? "month").ToLower())
                    {
                        case "quarter": fromDate = now.AddMonths(-3); break;
                        case "year": fromDate = new DateTime(now.Year, 1, 1); break;
                        default: fromDate = new DateTime(now.Year, now.Month, 1); break;
                    }
                }

                var q = _context.Transactions.Where(t => t.UserId == userId && t.Date >= fromDate && t.Date < toDate);
                if (categoryId.HasValue && categoryId.Value > 0)
                    q = q.Where(t => t.CategoryId == categoryId.Value);

                var txns = await q.ToListAsync();
                decimal totalIncome = txns.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal totalExpense = txns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                // 6-month trend
                var monthLabels = new List<string>();
                var incomeData = new List<decimal>();
                var expenseData = new List<decimal>();
                var baseMonth = new DateTime(now.Year, now.Month, 1);

                for (int i = 5; i >= 0; i--)
                {
                    var mStart = baseMonth.AddMonths(-i);
                    var mEnd = mStart.AddMonths(1);
                    monthLabels.Add(mStart.ToString("MMM"));

                    var monthQ = _context.Transactions.Where(t => t.UserId == userId && t.Date >= mStart && t.Date < mEnd);
                    if (categoryId.HasValue && categoryId.Value > 0)
                        monthQ = monthQ.Where(t => t.CategoryId == categoryId.Value);

                    var monthTxns = await monthQ.ToListAsync();
                    incomeData.Add(monthTxns.Where(t => t.Type == "Income").Sum(t => t.Amount));
                    expenseData.Add(monthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount));
                }

                // Category breakdown
                var expenseQuery = _context.Transactions.Include(t => t.Category)
                    .Where(t => t.UserId == userId && t.Type == "Expense" && t.Date >= fromDate && t.Date < toDate);
                if (categoryId.HasValue && categoryId.Value > 0)
                    expenseQuery = expenseQuery.Where(t => t.CategoryId == categoryId.Value);

                var expenseTxns = await expenseQuery.ToListAsync();
                var catGroups = expenseTxns
                    .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                    .Select(g => new { name = g.Key, total = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.total).ToList();

                var palette = new[] { "#38bdf8", "#0ea5e9", "#6366f1", "#818cf8", "#a78bfa", "#22d3ee", "#f59e0b", "#10b981" };
                var catLabels = catGroups.Select(c => c.name).ToList();
                var catValues = catGroups.Select(c => (double)c.total).ToList();
                var catColors = catLabels.Select((_, i) => palette[i % palette.Length]).ToList();

                // Daily activity last 7 days
                var activityLabels = new List<string>();
                var activityData = new List<int>();
                for (int i = 6; i >= 0; i--)
                {
                    var day = now.Date.AddDays(-i);
                    var nextDay = day.AddDays(1);
                    activityLabels.Add(day.ToString("ddd"));
                    activityData.Add(await _context.Transactions
                        .CountAsync(t => t.UserId == userId && t.Date >= day && t.Date < nextDay));
                }

                var allCats = await _context.Categories
                    .Where(c => c.IsDefault || c.UserId == userId)
                    .Select(c => new { c.CategoryId, c.Name, c.Type })
                    .ToListAsync();

                return Ok(new
                {
                    summary = new { totalIncome, totalExpense, balance = totalIncome - totalExpense, count = txns.Count },
                    incomeTrend = new { labels = monthLabels, income = incomeData, expense = expenseData },
                    categoryBreakdown = new { labels = catLabels, values = catValues, colors = catColors },
                    dailyActivity = new { labels = activityLabels, data = activityData },
                    allCategories = allCats,
                    filter = new
                    {
                        fromDate = fromDate.ToString("yyyy-MM-dd"),
                        toDate = toDate.AddDays(-1).ToString("yyyy-MM-dd")
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // ============== SAVING TIPS (SRS Page 8) ====================
        // ============================================================

        [HttpGet("/api/student/saving-tips")]
        public async Task<IActionResult> GetSavingTips()
        {
            int userId = GetCurrentUserId();
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var monthExpenses = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.Date >= monthStart)
                .ToListAsync();

            var tips = new List<object>();

            // Tip 1: Top spending category
            var topCat = monthExpenses
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .Select(g => new { name = g.Key, total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.total).FirstOrDefault();

            if (topCat != null)
            {
                tips.Add(new
                {
                    icon = "fa-chart-pie",
                    color = "#38bdf8",
                    title = $"Top spending: {topCat.name}",
                    text = $"You spent ${topCat.total:F2} on {topCat.name} this month. Try to reduce by 10% next month to save ${(topCat.total * 0.1m):F2}."
                });
            }

            // Tip 2: Over-budget check
            var budgets = await _context.Budgets.Where(b => b.UserId == userId).ToListAsync();
            var allExp = await _context.Transactions.Where(t => t.UserId == userId && t.Type == "Expense").ToListAsync();
            int overCount = 0;
            foreach (var b in budgets)
            {
                var spent = allExp.Where(t => t.CategoryId == b.CategoryId).Sum(t => t.Amount);
                if (b.LimitAmount > 0 && spent > b.LimitAmount) overCount++;
            }
            if (overCount > 0)
            {
                tips.Add(new
                {
                    icon = "fa-triangle-exclamation",
                    color = "#f59e0b",
                    title = $"{overCount} budget(s) exceeded",
                    text = $"You have crossed your budget limit in {overCount} categor{(overCount > 1 ? "ies" : "y")}. Review and adjust your spending pattern."
                });
            }

            // Tip 3: Savings tip
            var monthIncome = await _context.Transactions
                .Where(t => t.UserId == userId && t.Type == "Income" && t.Date >= monthStart)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            if (monthIncome > 0)
            {
                decimal savingsRate = ((monthIncome - monthExpenses.Sum(t => t.Amount)) / monthIncome) * 100;
                if (savingsRate < 20)
                {
                    tips.Add(new
                    {
                        icon = "fa-piggy-bank",
                        color = "#10b981",
                        title = $"Savings rate at {savingsRate:F1}%",
                        text = $"Aim to save at least 20% of your income (${(monthIncome * 0.2m):F2}). You're currently below the recommended target."
                    });
                }
                else
                {
                    tips.Add(new
                    {
                        icon = "fa-circle-check",
                        color = "#10b981",
                        title = $"Great! {savingsRate:F1}% savings rate",
                        text = $"You're saving well this month. Keep it up to reach your financial goals faster."
                    });
                }
            }

            // Tip 4: Category diversification
            if (monthExpenses.Count > 0)
            {
                int distinctCats = monthExpenses.Select(t => t.CategoryId).Distinct().Count();
                if (distinctCats <= 2)
                {
                    tips.Add(new
                    {
                        icon = "fa-layer-group",
                        color = "#6366f1",
                        title = "Diversify your expense tracking",
                        text = $"You spent across only {distinctCats} categor{(distinctCats > 1 ? "ies" : "y")} this month. Track more categories for better insights."
                    });
                }
            }

            return Ok(tips);
        }

        // ============================================================
        // ============== AI MONTHLY INSIGHT (SRS Page 8) =============
        // ============================================================

        [HttpGet("/api/student/monthly-insight")]
        public async Task<IActionResult> GetMonthlyInsight()
        {
            int userId = GetCurrentUserId();
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var prevStart = monthStart.AddMonths(-1);

            var thisMonth = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= monthStart)
                .ToListAsync();

            var lastMonth = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= prevStart && t.Date < monthStart)
                .ToListAsync();

            decimal thisExp = thisMonth.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal lastExp = lastMonth.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            decimal changePercent = lastExp > 0 ? ((thisExp - lastExp) / lastExp) * 100 : 0;

            var thisCats = thisMonth.Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var lastCats = lastMonth.Where(t => t.Type == "Expense")
                .GroupBy(t => t.Category != null ? t.Category.Name : "Other")
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // Find biggest increase
            string bigCat = null;
            decimal bigDelta = 0;
            foreach (var kv in thisCats)
            {
                decimal prev = lastCats.ContainsKey(kv.Key) ? lastCats[kv.Key] : 0;
                decimal delta = kv.Value - prev;
                if (Math.Abs(delta) > Math.Abs(bigDelta))
                {
                    bigCat = kv.Key;
                    bigDelta = delta;
                }
            }

            string summaryText;
            if (bigCat != null && bigDelta > 0)
                summaryText = $"📈 Your {bigCat} spending increased by ${bigDelta:F2} this month. Consider reducing it by 15% next month.";
            else if (bigCat != null && bigDelta < 0)
                summaryText = $"📉 Great! Your {bigCat} spending dropped by ${Math.Abs(bigDelta):F2}. Keep it up!";
            else
                summaryText = $"📊 Your expenses changed by {changePercent:F1}% this month compared to last month.";

            var actionableTip = changePercent > 10
                ? $"Aim to cut $({(thisExp * 0.1m):F2}) from your top category next month to bring your expense back in line."
                : "You're on track. Consider adding the difference to a saving goal.";

            return Ok(new
            {
                monthName = monthStart.ToString("MMMM yyyy"),
                thisMonthExpense = thisExp,
                lastMonthExpense = lastExp,
                changePercent = Math.Round(changePercent, 1),
                summaryText,
                actionableTip,
                topCategories = thisCats.OrderByDescending(x => x.Value).Take(5).Select(x => new { name = x.Key, amount = x.Value }).ToList()
            });
        }
    }

    // ============================================================
    // ============== REQUEST DTOs ================================
    // ============================================================

    public class StudentTransactionRequest
    {
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense";
        public DateTime Date { get; set; }
    }

    public class StudentCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense";
    }

    public class StudentBudgetRequest
    {
        public int CategoryId { get; set; }
        public decimal Limit { get; set; }
    }

    public class StudentGoalRequest
    {
        public string Title { get; set; } = string.Empty;
        public decimal Target { get; set; }
        public decimal Saved { get; set; }
        public DateTime Date { get; set; }
    }

    public class StudentDepositRequest
    {
        public decimal Amount { get; set; }
    }
}