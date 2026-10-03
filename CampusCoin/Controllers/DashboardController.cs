using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Models.DTOs;
using CampusCoin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FinancialHealthService _healthService;

        public DashboardController(ApplicationDbContext context, FinancialHealthService healthService)
        {
            _context = context;
            _healthService = healthService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: / (Dashboard View) =================
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
        // ================= GET: /Dashboard/Profile =================  ? YEH ADD KARO
        [HttpGet]
        public IActionResult Profile()
        {
            return View();
        }
        // ================= GET: /Dashboard/Budgets =================
        [HttpGet]
        public IActionResult Budgets()
        {
            return View();
        }

        // ================= GET: /Dashboard/Categories =================
        [HttpGet]
        public IActionResult Categories()
        {
            return View();
        }
        // ================= GET: /Dashboard/FinancialHealth =================
        [HttpGet]
        public IActionResult FinancialHealth()
        {
            return View();
        }
        // ================= GET: /Dashboard/SavingGoals =================
        [HttpGet]
        public IActionResult SavingGoals()
        {
            return View();
        }

        // ================= GET: /Dashboard/Notifications =================
        [HttpGet]
        public IActionResult Notifications()
        {
            return View();
        }
        // ================= GET: /Dashboard/Transactions =================
        [HttpGet]
        public IActionResult Transactions()
        {
            return View();
        }

        // ================= GET: /Dashboard/Reports =================
        [HttpGet]
        public IActionResult Reports()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Comparison()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Recurring()
        {
            return View();
        }
        [HttpGet]
                public IActionResult Bookmarks()
        {
            return View();
        }

        // ================= GET: /Dashboard/BillSplit =================
        public IActionResult BillSplit()
        {
            return View();
        }

        // Student events page — redirect to main Events controller view
        [HttpGet("/Dashboard/Events")]
        public IActionResult Events()
        {
            return Redirect("/Events");
        }

        // ================= EVENTS VIEW =================
        //[HttpGet]
        //public IActionResult Events()
        //{
        //    return View();
        //}
        // ================= EVENTS: Get All =================
        //[HttpGet("/api/dashboard/events")]
        //public async Task<IActionResult> GetEvents()
        //{
        //    int userId = GetCurrentUserId();

        //    var events = await _context.Events
        //        .OrderByDescending(e => e.EventDate)
        //        .ToListAsync();

        //    var myRegs = await _context.EventRegistrations
        //        .Where(r => r.UserId == userId)
        //        .ToListAsync();

        //    var result = events.Select(e => new
        //    {
        //        id = e.Id,
        //        eventCode = e.EventCode,
        //        title = e.Title,
        //        category = e.Category,
        //        status = e.Status,
        //        image = e.Image,
        //        date = e.EventDate.ToString("MMM dd, yyyy"),
        //        eventDate = e.EventDate.ToString("yyyy-MM-dd"),
        //        time = e.Time,
        //        venue = e.Venue,
        //        fee = e.Fee,
        //        seatsTotal = e.SeatsTotal,
        //        seatsLeft = e.SeatsLeft,
        //        description = e.Description,
        //        registered = myRegs.Any(r => r.EventId == e.Id),
        //        passCode = myRegs.FirstOrDefault(r => r.EventId == e.Id)?.PassCode
        //    });

        //    return Ok(result);
        //}

        // ================= EVENTS: Register =================
        //[HttpPost("/api/dashboard/events/{id}/register")]
        //[IgnoreAntiforgeryToken]
        //public async Task<IActionResult> RegisterEvent(int id)
        //{
        //    try
        //    {
        //        int userId = GetCurrentUserId();

        //        var evt = await _context.Events.FindAsync(id);
        //        if (evt == null)
        //            return NotFound(new { success = false, message = "Event not found" });

        //        var alreadyReg = await _context.EventRegistrations
        //            .AnyAsync(r => r.UserId == userId && r.EventId == id);
        //        if (alreadyReg)
        //            return BadRequest(new { success = false, message = "Already registered" });

        //        if (evt.SeatsLeft <= 0)
        //            return BadRequest(new { success = false, message = "Event is full" });

        //        var user = await _context.Users.FindAsync(userId);
        //        if (user != null && evt.Fee > 0)
        //        {
        //            if (user.MonthlyAllowanceBaseline < evt.Fee)
        //                return BadRequest(new { success = false, message = "Insufficient wallet balance" });

        //            user.MonthlyAllowanceBaseline -= evt.Fee;

        //            _context.Transactions.Add(new Transaction
        //            {
        //                UserId = userId,
        //                CategoryId = 11,
        //                Amount = evt.Fee,
        //                Type = "Expense",
        //                Description = $"Event: {evt.Title}",
        //                Date = DateTime.UtcNow,
        //                CreatedAt = DateTime.UtcNow
        //            });
        //        }

        //        evt.SeatsLeft--;

        //        var passCode = $"PASS-{new Random().Next(1000, 9999)}-{evt.Id}";
        //        var reg = new EventRegistration
        //        {
        //            EventId = id,
        //            UserId = userId,
        //            PassCode = passCode,
        //            RegisteredAt = DateTime.UtcNow
        //        };
        //        _context.EventRegistrations.Add(reg);

        //        await _context.SaveChangesAsync();

        //        return Ok(new { success = true, message = "Registered successfully", passCode });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { success = false, message = ex.Message });
        //    }
        //}

        // ================= GET: /api/dashboard/summary =================
        [HttpGet("/api/dashboard/summary")]
        public async Task<IActionResult> GetSummary()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);
            var startOfLastMonth = startOfMonth.AddMonths(-1);

            // All-time balance
            var allTxns = await _context.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();

            decimal totalAllIncome = allTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalAllExpenses = allTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal totalBalance = totalAllIncome - totalAllExpenses;

            // Current Month
            var thisMonthTxns = allTxns.Where(t => t.Date >= startOfMonth && t.Date < endOfMonth).ToList();
            decimal monthlyIncome = thisMonthTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal monthlyExpenses = thisMonthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            if (monthlyIncome == 0 && user.MonthlyAllowanceBaseline > 0)
            {
                monthlyIncome = user.MonthlyAllowanceBaseline;
            }

            // Last Month
            var lastMonthTxns = allTxns.Where(t => t.Date >= startOfLastMonth && t.Date < startOfMonth).ToList();
            decimal lastMonthExpenses = lastMonthTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

            decimal balanceChangePercent = 0;
            if (lastMonthExpenses > 0)
            {
                balanceChangePercent = Math.Round(((monthlyExpenses - lastMonthExpenses) / lastMonthExpenses) * 100m, 1);
            }

            // Budget usage percentage
            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId && b.Month == startOfMonth)
                .ToListAsync();

            decimal budgetUsage = 0;
            if (budgets.Count != 0 && budgets.Sum(b => b.LimitAmount) > 0)
            {
                var budgetedCategoryIds = budgets.Select(b => b.CategoryId).ToList();
                decimal budgetedSpent = thisMonthTxns
                    .Where(t => t.Type == "Expense" && budgetedCategoryIds.Contains(t.CategoryId))
                    .Sum(t => t.Amount);

                budgetUsage = Math.Round((budgetedSpent / budgets.Sum(b => b.LimitAmount)) * 100m, 1);
            }
            else if (user.MonthlyAllowanceBaseline > 0)
            {
                budgetUsage = Math.Round((monthlyExpenses / user.MonthlyAllowanceBaseline) * 100m, 1);
            }

            // Health Score
            var (healthScore, healthStatus, breakdown) = await _healthService.CalculateHealthScoreAsync(userId, _context, now);

            // Unread notifications
            int unreadCount = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();

            return Ok(new DashboardSummaryDto
            {
                TotalBalance = totalBalance,
                MonthlyIncome = monthlyIncome,
                MonthlyExpenses = monthlyExpenses,
                HealthScore = healthScore,
                HealthScoreStatus = healthStatus,
                HealthBreakdown = breakdown,
                BudgetUsagePercentage = budgetUsage,
                BalanceChangePercent = balanceChangePercent,
                UnreadNotificationsCount = unreadCount,
                UserName = user.FullName,
                UserAcademicYear = user.AcademicYear ?? "Student",
                AllowanceBaseline = user.MonthlyAllowanceBaseline
            });
        }

        // ================= GET: /api/dashboard/charts =================
        [HttpGet("/api/dashboard/charts")]
        public async Task<IActionResult> GetCharts()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);

            var charts = new DashboardChartsDto();

            // Category color map
            var colorPalette = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Food", "#f59e0b" },          // amber
                { "Transport", "#38bdf8" },     // sky
                { "Academics", "#c084fc" },     // purple
                { "Entertainment", "#f472b6" }, // pink
                { "Hostel/Rent", "#6366f1" },   // indigo
                { "Subscriptions", "#ec4899" }, // rose
                { "Miscellaneous", "#94a3b8" }, // slate
                { "Allowance", "#10b981" },     // emerald
                { "Part-time Job", "#06b6d4" }, // cyan
                { "Scholarship", "#8b5cf6" },   // violet
                { "Gift", "#3b82f6" },          // blue
                { "Other Income", "#14b8a6" }   // teal
            };

            // 1 & 5. 6-Month Cashflow Trajectory & Grouped Income vs Expense
            var sixMonthsStart = currentMonthStart.AddMonths(-5);
            var sixMonthsTxns = await _context.Transactions
                .Where(t => t.UserId == userId && t.Date >= sixMonthsStart && t.Date < currentMonthStart.AddMonths(1))
                .ToListAsync();

            for (int i = 5; i >= 0; i--)
            {
                var mStart = currentMonthStart.AddMonths(-i);
                var mEnd = mStart.AddMonths(1);
                string label = mStart.ToString("MMM");

                var mTxns = sixMonthsTxns.Where(t => t.Date >= mStart && t.Date < mEnd).ToList();
                decimal inc = mTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
                decimal exp = mTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                if (inc == 0 && user != null && user.MonthlyAllowanceBaseline > 0)
                {
                    inc = user.MonthlyAllowanceBaseline;
                }

                charts.Cashflow.Labels.Add(label);
                charts.Cashflow.IncomeData.Add(inc);
                charts.Cashflow.ExpenseData.Add(exp);

                charts.SixMonthComparison.Labels.Add(label);
                charts.SixMonthComparison.IncomeData.Add(inc);
                charts.SixMonthComparison.ExpenseData.Add(exp);
            }

            // Current Month Expenses by Category
            var currentMonthExpenses = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == "Expense" && t.Date >= currentMonthStart && t.Date < currentMonthStart.AddMonths(1))
                .ToListAsync();

            var catGroups = currentMonthExpenses
                .GroupBy(t => t.Category?.Name ?? "Other")
                .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Total)
                .ToList();

            // 2. Spending Donut
            if (catGroups.Count == 0)
            {
                charts.SpendingDonut.Labels.AddRange(new[] { "Food", "Transport", "Academics", "Entertainment" });
                charts.SpendingDonut.Data.AddRange(new[] { 180m, 85m, 140m, 65m });
                charts.SpendingDonut.Colors.AddRange(new[] { "#f59e0b", "#38bdf8", "#c084fc", "#f472b6" });
            }
            else
            {
                foreach (var cg in catGroups)
                {
                    charts.SpendingDonut.Labels.Add(cg.Name);
                    charts.SpendingDonut.Data.Add(cg.Total);
                    charts.SpendingDonut.Colors.Add(colorPalette.GetValueOrDefault(cg.Name, "#38bdf8"));
                }
            }

            // 3. Category-wise Bar Chart (Expenses by Category)
            foreach (var cg in catGroups.Take(7))
            {
                charts.CategoryBar.Labels.Add(cg.Name);
                charts.CategoryBar.Data.Add(cg.Total);
                charts.CategoryBar.Colors.Add(colorPalette.GetValueOrDefault(cg.Name, "#0ea5e9"));
            }

            // 4. Budget vs. Actual Bar Chart
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.Month == currentMonthStart)
                .ToListAsync();

            if (budgets.Count == 0)
            {
                // Demo fallback
                charts.BudgetVsActual.Labels.AddRange(new[] { "Food", "Transport", "Academics", "Entertainment" });
                charts.BudgetVsActual.BudgetLimits.AddRange(new[] { 250m, 100m, 200m, 80m });
                charts.BudgetVsActual.ActualSpent.AddRange(new[] { 180m, 85m, 140m, 65m });
            }
            else
            {
                foreach (var b in budgets)
                {
                    string catName = b.Category?.Name ?? "Budget";
                    decimal actual = currentMonthExpenses.Where(t => t.CategoryId == b.CategoryId).Sum(t => t.Amount);

                    charts.BudgetVsActual.Labels.Add(catName);
                    charts.BudgetVsActual.BudgetLimits.Add(b.LimitAmount);
                    charts.BudgetVsActual.ActualSpent.Add(actual);
                }
            }

            // 6. Weekly Spending Trend (Weeks 1 to 4)
            charts.WeeklyTrend.Labels.AddRange(new[] { "Week 1 (1-7)", "Week 2 (8-14)", "Week 3 (15-21)", "Week 4 (22-end)" });
            decimal w1 = currentMonthExpenses.Where(t => t.Date.Day <= 7).Sum(t => t.Amount);
            decimal w2 = currentMonthExpenses.Where(t => t.Date.Day >= 8 && t.Date.Day <= 14).Sum(t => t.Amount);
            decimal w3 = currentMonthExpenses.Where(t => t.Date.Day >= 15 && t.Date.Day <= 21).Sum(t => t.Amount);
            decimal w4 = currentMonthExpenses.Where(t => t.Date.Day >= 22).Sum(t => t.Amount);
            charts.WeeklyTrend.Data.AddRange(new[] { w1, w2, w3, w4 });

            // 7. Savings Goal Radial
            var activeGoal = await _context.SavingsGoals
                .Where(g => g.UserId == userId && !g.IsAchieved)
                .OrderByDescending(g => g.CurrentAmount)
                .FirstOrDefaultAsync();

            if (activeGoal != null && activeGoal.TargetAmount > 0)
            {
                charts.SavingsRadial.GoalName = activeGoal.GoalName;
                charts.SavingsRadial.TargetAmount = activeGoal.TargetAmount;
                charts.SavingsRadial.CurrentAmount = activeGoal.CurrentAmount;
                charts.SavingsRadial.Percentage = Math.Min(100, Math.Round((activeGoal.CurrentAmount / activeGoal.TargetAmount) * 100m, 1));
            }
            else
            {
                charts.SavingsRadial.GoalName = "Semester Buffer";
                charts.SavingsRadial.TargetAmount = 1600.00m;
                charts.SavingsRadial.CurrentAmount = 1480.00m;
                charts.SavingsRadial.Percentage = 92.5m;
            }

            // 8. Financial Health Breakdown (Radar / Sub-scores)
            var (_, _, breakdown) = await _healthService.CalculateHealthScoreAsync(userId, _context, now);
            charts.HealthRadar.Scores = new List<int>
            {
                breakdown.BudgetControlScore,
                breakdown.SavingRateScore,
                breakdown.GoalProgressScore,
                breakdown.ExpenseStabilityScore
            };

            // 9. Top 5 Spending Categories Horizontal Bar
            foreach (var cg in catGroups.Take(5))
            {
                charts.TopSpending.Labels.Add(cg.Name);
                charts.TopSpending.Data.Add(cg.Total);
                charts.TopSpending.Colors.Add(colorPalette.GetValueOrDefault(cg.Name, "#6366f1"));
            }

            return Ok(charts);
        }

        // ================= POST: /api/dashboard/simulate-affordability =================
        [HttpPost("/api/dashboard/simulate-affordability")]
        public async Task<IActionResult> SimulateAffordability([FromBody] AffordabilityRequestDto model)
        {
            if (!ModelState.IsValid || model.ItemCost <= 0)
            {
                return BadRequest(new { success = false, message = "Please enter a valid item cost." });
            }

            int userId = GetCurrentUserId();

            // Calculate live balance
            var allTxns = await _context.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();

            decimal totalIncome = allTxns.Where(t => t.Type == "Income").Sum(t => t.Amount);
            decimal totalExpense = allTxns.Where(t => t.Type == "Expense").Sum(t => t.Amount);
            decimal currentBalance = totalIncome - totalExpense;

            decimal safeBuffer = 300.00m; // Recommended student cushion
            decimal remaining = currentBalance - model.ItemCost;

            string status;
            string title;
            string description;
            bool isAffordable;
            bool safeBufferMaintained;

            string itemName = string.IsNullOrWhiteSpace(model.ItemName) ? "this purchase" : model.ItemName.Trim();

            if (remaining >= safeBuffer)
            {
                status = "Safe";
                isAffordable = true;
                safeBufferMaintained = true;
                title = $"Yes! You can safely buy {itemName}.";
                description = $"Post-purchase balance: ${remaining:F2} (Safe buffer of ${safeBuffer:F0}+ maintained).";
            }
            else if (remaining > 0)
            {
                status = "Caution";
                isAffordable = true;
                safeBufferMaintained = false;
                title = "Proceed with Caution!";
                description = $"Remaining balance (${remaining:F2}) dips below your ${safeBuffer:F0} emergency safety cushion.";
            }
            else
            {
                status = "Unaffordable";
                isAffordable = false;
                safeBufferMaintained = false;
                title = "Not Recommended Right Now";
                description = $"This purchase exceeds your current available funds by ${Math.Abs(remaining):F2}.";
            }

            // Log simulation
            _context.SimulationLogs.Add(new SimulationLog
            {
                UserId = userId,
                SimulationType = "CanIAffordIt",
                InputSummary = $"Item: {itemName}, Cost: ${model.ItemCost:F2}",
                ResultSummary = $"{status}: {title} | {description}",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new AffordabilityResponseDto
            {
                IsAffordable = isAffordable,
                Status = status,
                Title = title,
                Description = description,
                CurrentBalance = currentBalance,
                PostPurchaseBalance = remaining,
                SafeBuffer = safeBuffer,
                SafeBufferMaintained = safeBufferMaintained
            });
        }

        // ================= SMART SPENDING ALERTS =================
        [HttpGet("/api/dashboard/spending-alerts")]
        public async Task<IActionResult> GetSpendingAlerts()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.Month == monthStart && b.LimitAmount > 0)
                .ToListAsync();

            var alertRows = new List<(int categoryId, string category, decimal limit, decimal spent, decimal remaining, decimal percent, string severity, string message)>();
            foreach (var b in budgets)
            {
                var spent = await _context.Transactions
                    .Where(t => t.UserId == userId && t.CategoryId == b.CategoryId
                        && t.Type == "Expense" && t.Date >= monthStart && t.Date < monthEnd)
                    .SumAsync(t => t.Amount);

                if (b.LimitAmount <= 0) continue;
                var pct = Math.Round(spent / b.LimitAmount * 100m, 0);
                if (pct < 80) continue;

                var remaining = b.LimitAmount - spent;
                var severity = pct >= 100 ? "over" : "warning";
                var catName = b.Category?.Name ?? "Category";
                string msg = pct >= 100
                    ? $"You've used {pct}% of your {catName} budget — over by ${Math.Abs(remaining):F2}."
                    : $"You've used {pct}% of your {catName} budget — ${remaining:F2} remaining this month.";

                alertRows.Add((b.CategoryId, catName, b.LimitAmount, spent, remaining, pct, severity, msg));
            }

            var alerts = alertRows
                .OrderByDescending(a => a.severity == "over")
                .ThenByDescending(a => a.percent)
                .Select(a => new
                {
                    categoryId = a.categoryId,
                    category = a.category,
                    limit = a.limit,
                    spent = a.spent,
                    remaining = a.remaining,
                    percent = a.percent,
                    severity = a.severity,
                    message = a.message
                })
                .ToList();

            return Ok(new { success = true, alerts });
        }

        // ================= EXPENSE COMPARISON (This vs Last Month) =================
        [HttpGet("/api/dashboard/expense-comparison")]
        public async Task<IActionResult> GetExpenseComparison()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var now = DateTime.UtcNow;
            var thisStart = new DateTime(now.Year, now.Month, 1);
            var thisEnd = thisStart.AddMonths(1);
            var lastStart = thisStart.AddMonths(-1);
            var lastEnd = thisStart;

            var thisMonth = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == "Expense"
                    && t.Date >= thisStart && t.Date < thisEnd)
                .GroupBy(t => new { t.CategoryId, Name = t.Category != null ? t.Category.Name : "Other" })
                .Select(g => new { g.Key.CategoryId, g.Key.Name, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var lastMonth = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == "Expense"
                    && t.Date >= lastStart && t.Date < lastEnd)
                .GroupBy(t => new { t.CategoryId, Name = t.Category != null ? t.Category.Name : "Other" })
                .Select(g => new { g.Key.CategoryId, g.Key.Name, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var names = thisMonth.Select(x => x.Name)
                .Union(lastMonth.Select(x => x.Name))
                .OrderBy(n => n)
                .ToList();

            var categories = new List<object>();
            var labels = new List<string>();
            var thisData = new List<decimal>();
            var lastData = new List<decimal>();

            foreach (var name in names)
            {
                var t = thisMonth.FirstOrDefault(x => x.Name == name)?.Total ?? 0;
                var l = lastMonth.FirstOrDefault(x => x.Name == name)?.Total ?? 0;
                decimal changePct = 0;
                if (l > 0) changePct = Math.Round((t - l) / l * 100m, 1);
                else if (t > 0) changePct = 100;

                labels.Add(name);
                thisData.Add(t);
                lastData.Add(l);
                categories.Add(new
                {
                    category = name,
                    thisMonth = t,
                    lastMonth = l,
                    changePercent = changePct,
                    direction = changePct > 0 ? "up" : (changePct < 0 ? "down" : "same")
                });
            }

            return Ok(new
            {
                success = true,
                labels,
                thisMonth = thisData,
                lastMonth = lastData,
                categories,
                thisMonthLabel = thisStart.ToString("MMM yyyy"),
                lastMonthLabel = lastStart.ToString("MMM yyyy")
            });
        }

    }
}
