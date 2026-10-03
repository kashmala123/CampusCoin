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
    public class TransactionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TransactionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/transactions =================
        [HttpGet]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] string? month = null,
            [FromQuery] string? category = null,
            [FromQuery] string? search = null,
            [FromQuery] string? type = null)
        {
            int userId = GetCurrentUserId();
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (!string.IsNullOrWhiteSpace(month) && month != "ALL" && DateTime.TryParse(month + "-01", out var monthDate))
            {
                var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1);
                query = query.Where(t => t.Date >= startOfMonth && t.Date < endOfMonth);
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "ALL")
            {
                if (int.TryParse(category, out int catId))
                {
                    query = query.Where(t => t.CategoryId == catId);
                }
                else
                {
                    query = query.Where(t => t.Category != null && t.Category.Name.ToLower() == category.Trim().ToLower());
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(t => (t.Description != null && t.Description.ToLower().Contains(searchLower)) ||
                                         (t.Category != null && t.Category.Name.ToLower().Contains(searchLower)));
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
            {
                query = query.Where(t => t.Type.ToLower() == type.Trim().ToLower());
            }

            var list = await query
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.TransactionId)
                .Select(t => new TransactionDto
                {
                    Id = t.TransactionId,
                    Title = t.Description ?? (t.Category != null ? t.Category.Name : "Transaction"),
                    CategoryId = t.CategoryId,
                    Category = t.Category != null ? t.Category.Name : "Uncategorized",
                    Date = t.Date.ToString("yyyy-MM-dd"),
                    Amount = t.Amount,
                    Type = t.Type
                })
                .ToListAsync();

            return Ok(list);
        }

        // ================= GET: /api/transactions/{id} =================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransaction(int id)
        {
            int userId = GetCurrentUserId();
            var t = await _context.Transactions
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.TransactionId == id && x.UserId == userId);

            if (t == null) return NotFound(new { message = "Transaction not found." });

            return Ok(new TransactionDto
            {
                Id = t.TransactionId,
                Title = t.Description ?? (t.Category?.Name ?? "Transaction"),
                CategoryId = t.CategoryId,
                Category = t.Category?.Name ?? "Uncategorized",
                Date = t.Date.ToString("yyyy-MM-dd"),
                Amount = t.Amount,
                Type = t.Type
            });
        }

        // ================= POST: /api/transactions =================
        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Please fill in all transaction fields." });
            }

            int userId = GetCurrentUserId();

            Category? cat = null;
            string wantType = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";
            if (int.TryParse(model.Category, out int catId))
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == catId && (c.UserId == null || c.UserId == userId));
            }
            // Prefer category with same name AND matching Income/Expense type
            if (cat == null)
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == model.Category.Trim().ToLower() &&
                    c.Type == wantType &&
                    (c.UserId == null || c.UserId == userId));
            }
            if (cat == null)
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == model.Category.Trim().ToLower() &&
                    (c.UserId == null || c.UserId == userId));
            }
            // If found category is wrong type (e.g. Expense cat used for Income), create matching type
            if (cat != null && !string.Equals(cat.Type, wantType, StringComparison.OrdinalIgnoreCase))
            {
                var typed = await _context.Categories.FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == model.Category.Trim().ToLower() &&
                    c.Type == wantType &&
                    (c.UserId == null || c.UserId == userId));
                if (typed != null) cat = typed;
                else
                {
                    cat = new Category
                    {
                        Name = model.Category.Trim(),
                        Type = wantType,
                        IsDefault = false,
                        UserId = userId
                    };
                    _context.Categories.Add(cat);
                    await _context.SaveChangesAsync();
                }
            }
            if (cat == null)
            {
                cat = new Category
                {
                    Name = model.Category.Trim(),
                    Type = wantType,
                    IsDefault = false,
                    UserId = userId
                };
                _context.Categories.Add(cat);
                await _context.SaveChangesAsync();
            }

            DateTime txnDate = DateTime.UtcNow.Date;
            if (!string.IsNullOrWhiteSpace(model.Date) && DateTime.TryParse(model.Date, out var parsedDate))
            {
                txnDate = parsedDate.Date;
            }

            // Flag unusually large expenses vs recent average
            decimal amt = Math.Abs(model.Amount);
            bool flagLarge = false;
            if (model.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase))
            {
                var recentExp = await _context.Transactions
                    .Where(t => t.UserId == userId && t.Type == "Expense")
                    .OrderByDescending(t => t.Date)
                    .Take(30)
                    .Select(t => t.Amount)
                    .ToListAsync();
                if (recentExp.Count >= 3)
                {
                    var avg = recentExp.Average();
                    if (avg > 0 && amt >= avg * 2.5m) flagLarge = true;
                }
            }

            var transaction = new Transaction
            {
                UserId = userId,
                CategoryId = cat.CategoryId,
                Amount = amt,
                Type = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense",
                Description = model.Title.Trim(),
                Date = txnDate,
                CreatedAt = DateTime.UtcNow,
                IsFlagged = flagLarge,
                AiSuggestedCategoryId = model.SuggestedCategoryId
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            if (model.IsRecurring)
            {
                _context.RecurringTransactions.Add(new RecurringTransaction
                {
                    UserId = userId,
                    CategoryId = cat.CategoryId,
                    Amount = transaction.Amount,
                    Type = transaction.Type,
                    Description = transaction.Description,
                    Frequency = model.RecurringFrequency ?? "Monthly",
                    NextRunDate = txnDate.AddMonths(1),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                TransactionId = transaction.TransactionId,
                ActionType = "Created",
                Notes = $"Added {transaction.Type} '{transaction.Description}' of ${transaction.Amount:F2}",
                CreatedAt = DateTime.UtcNow
            });

            string? budgetAlertMessage = null;
            string? budgetAlertSeverity = null;
            decimal? budgetAlertPercent = null;
            decimal? budgetRemaining = null;

            if (transaction.Type == "Expense")
            {
                var monthStart = new DateTime(txnDate.Year, txnDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);
                var budget = await _context.Budgets
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == cat.CategoryId && b.Month == monthStart);

                if (budget != null && budget.LimitAmount > 0)
                {
                    var totalSpentInCat = await _context.Transactions
                        .Where(t => t.UserId == userId && t.CategoryId == cat.CategoryId && t.Type == "Expense" && t.Date >= monthStart && t.Date < monthEnd)
                        .SumAsync(t => t.Amount);

                    decimal ratio = totalSpentInCat / budget.LimitAmount;
                    decimal remaining = budget.LimitAmount - totalSpentInCat;
                    budgetAlertPercent = Math.Round(ratio * 100m, 0);
                    budgetRemaining = remaining;

                    if (ratio >= 1.0m)
                    {
                        budgetAlertSeverity = "over";
                        budgetAlertMessage = $"⚠️ You've used {budgetAlertPercent}% of your {cat.Name} budget — over by ${Math.Abs(remaining):F2} this month.";
                        _context.Notifications.Add(new Notification
                        {
                            UserId = userId,
                            Message = $"Alert: Your {cat.Name} spending (${totalSpentInCat:F2}) has exceeded your monthly budget (${budget.LimitAmount:F2})!",
                            Type = "Alert",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else if (ratio >= 0.80m)
                    {
                        budgetAlertSeverity = "warning";
                        budgetAlertMessage = $"⚠️ You've used {budgetAlertPercent}% of your {cat.Name} budget — ${remaining:F2} remaining this month.";
                        _context.Notifications.Add(new Notification
                        {
                            UserId = userId,
                            Message = $"Warning: Your {cat.Name} budget is at {(ratio * 100):F0}% (${totalSpentInCat:F2} / ${budget.LimitAmount:F2}).",
                            Type = "Warning",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            
                // Log when user picks a different category than suggested
                if (model.SuggestedCategoryId.HasValue && model.SuggestedCategoryId.Value != cat.CategoryId)
                {
                    _context.CategoryCorrections.Add(new CategoryCorrection
                    {
                        TransactionId = transaction.TransactionId,
                        UserId = userId,
                        SuggestedCategoryId = model.SuggestedCategoryId,
                        ActualCategoryId = cat.CategoryId,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }

                return Ok(new TransactionDto
            {
                Id = transaction.TransactionId,
                Title = transaction.Description,
                CategoryId = cat.CategoryId,
                Category = cat.Name,
                Date = transaction.Date.ToString("yyyy-MM-dd"),
                Amount = transaction.Amount,
                Type = transaction.Type,
                IsRecurring = model.IsRecurring,
                BudgetAlertMessage = budgetAlertMessage,
                BudgetAlertSeverity = budgetAlertSeverity,
                BudgetAlertPercent = budgetAlertPercent,
                BudgetRemaining = budgetRemaining
            });
        }

        // ================= PUT: /api/transactions/{id} =================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTransaction(int id, [FromBody] UpdateTransactionDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid update data." });
            }

            int userId = GetCurrentUserId();
            var transaction = await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (transaction == null)
            {
                return NotFound(new { success = false, message = "Transaction not found or unauthorized." });
            }

            _context.TransactionHistories.Add(new TransactionHistory
            {
                TransactionId = transaction.TransactionId,
                UserId = userId,
                ChangeType = "Edited",
                PreviousAmount = transaction.Amount,
                PreviousCategoryId = transaction.CategoryId,
                PreviousType = transaction.Type,
                PreviousDescription = transaction.Description,
                PreviousDate = transaction.Date,
                ChangedAt = DateTime.UtcNow
            });

            Category? cat = null;
            if (int.TryParse(model.Category, out int catId))
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == catId && (c.UserId == null || c.UserId == userId));
            }
            if (cat == null)
            {
                cat = await _context.Categories.FirstOrDefaultAsync(c =>
                    c.Name.ToLower() == model.Category.Trim().ToLower() &&
                    (c.UserId == null || c.UserId == userId));
            }

            if (cat != null)
            {
                transaction.CategoryId = cat.CategoryId;
            }

            transaction.Description = model.Title.Trim();
            transaction.Amount = Math.Abs(model.Amount);
            transaction.Type = model.Type.Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";

            if (!string.IsNullOrWhiteSpace(model.Date) && DateTime.TryParse(model.Date, out var parsedDate))
            {
                transaction.Date = parsedDate.Date;
            }

            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                TransactionId = transaction.TransactionId,
                ActionType = "Edited",
                Notes = $"Updated transaction #{id}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new TransactionDto
            {
                Id = transaction.TransactionId,
                Title = transaction.Description,
                CategoryId = transaction.CategoryId,
                Category = transaction.Category?.Name ?? cat?.Name ?? "Uncategorized",
                Date = transaction.Date.ToString("yyyy-MM-dd"),
                Amount = transaction.Amount,
                Type = transaction.Type
            });
        }

        // ================= DELETE: /api/transactions/{id} =================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            int userId = GetCurrentUserId();
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

            if (transaction == null)
            {
                return NotFound(new { success = false, message = "Transaction not found or unauthorized." });
            }

            try
            {
                // ============================================================
                // STEP 1: Find all tables referencing Transactions (FK lookup)
                // ============================================================
                var fkTables = new List<(string Table, string Column)>();
                try
                {
                    using var conn = _context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT DISTINCT OBJECT_NAME(fk.parent_object_id),
                                        COL_NAME(fkc.parent_object_id, fkc.parent_column_id)
                        FROM sys.foreign_keys fk
                        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        WHERE OBJECT_NAME(fk.referenced_object_id) = 'Transactions'";

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                            fkTables.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }
                catch { }

                // ============================================================
                // STEP 2: Detach or delete each dependent row
                // ============================================================
                foreach (var (table, column) in fkTables)
                {
                    try
                    {
                        // Try NULL-ing the FK column
                        await _context.Database.ExecuteSqlRawAsync(
                            $"UPDATE [{table}] SET [{column}] = NULL WHERE [{column}] = {{0}}", id);
                    }
                    catch
                    {
                        // If column is NOT NULL, delete the row instead
                        try
                        {
                            await _context.Database.ExecuteSqlRawAsync(
                                $"DELETE FROM [{table}] WHERE [{column}] = {{0}}", id);
                        }
                        catch { }
                    }
                }

                // ============================================================
                // STEP 3: Delete the transaction via raw SQL (bypasses EF)
                // ============================================================
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM Transactions WHERE TransactionId = {0}", id);

                // ============================================================
                // STEP 4: Log activity (no FK reference)
                // ============================================================
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = userId,
                    TransactionId = null,
                    ActionType = "Deleted",
                    Notes = $"Deleted transaction #{id} ('{transaction.Description}')",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Transaction successfully deleted." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    stage = "delete",
                    message = ex.Message,
                    inner = ex.InnerException?.Message,
                    type = ex.GetType().Name
                });
            }
        }

        
        
        // ================= GET: /api/transactions/anomalies =================
        /// <summary>SRS optional: flag unusually large or duplicate transactions.</summary>
        [HttpGet("anomalies")]
        public async Task<IActionResult> GetAnomalies()
        {
            int userId = GetCurrentUserId();
            var recent = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .Take(200)
                .ToListAsync();

            if (recent.Count == 0)
                return Ok(new { success = true, large = Array.Empty<object>(), duplicates = Array.Empty<object>() });

            var expenseAmounts = recent.Where(t => t.Type == "Expense").Select(t => t.Amount).ToList();
            decimal avg = expenseAmounts.Count > 0 ? expenseAmounts.Average() : 0;
            decimal threshold = avg > 0 ? avg * 2.5m : 5000m;

            var large = recent
                .Where(t => t.Type == "Expense" && t.Amount >= threshold)
                .Take(10)
                .Select(t => new {
                    id = t.TransactionId,
                    title = t.Description,
                    amount = t.Amount,
                    date = t.Date.ToString("yyyy-MM-dd"),
                    category = t.Category != null ? t.Category.Name : "Other",
                    reason = $"Unusually large (avg ~{avg:F0}, this is {t.Amount:F0})"
                })
                .ToList();

            var duplicates = recent
                .GroupBy(t => new { t.Amount, Desc = (t.Description ?? "").Trim().ToLowerInvariant(), Day = t.Date.Date })
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Select(t => new {
                    id = t.TransactionId,
                    title = t.Description,
                    amount = t.Amount,
                    date = t.Date.ToString("yyyy-MM-dd"),
                    category = t.Category != null ? t.Category.Name : "Other",
                    reason = "Possible duplicate (same amount, description, and day)"
                }))
                .Take(15)
                .ToList();

            return Ok(new { success = true, large, duplicates, averageExpense = avg, largeThreshold = threshold });
        }

        // ================= GET: /api/transactions/forecast =================
        /// <summary>SRS optional: simple next-month forecast from history.</summary>
        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast()
        {
            int userId = GetCurrentUserId();
            var start = DateTime.UtcNow.Date.AddMonths(-3);
            var txns = await _context.Transactions
                .Where(t => t.UserId == userId && t.Date >= start)
                .ToListAsync();

            var months = Enumerable.Range(0, 3).Select(i => {
                var mStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
                var mEnd = mStart.AddMonths(1);
                var slice = txns.Where(t => t.Date >= mStart && t.Date < mEnd).ToList();
                return new {
                    month = mStart.ToString("yyyy-MM"),
                    income = slice.Where(t => t.Type == "Income").Sum(t => t.Amount),
                    expense = slice.Where(t => t.Type == "Expense").Sum(t => t.Amount)
                };
            }).ToList();

            decimal avgIncome = months.Count > 0 ? months.Average(m => m.income) : 0;
            decimal avgExpense = months.Count > 0 ? months.Average(m => m.expense) : 0;
            var nextMonth = DateTime.UtcNow.Date.AddMonths(1);
            var label = nextMonth.ToString("MMMM yyyy");

            // Persist optional Forecast row at most once per user / target month
            try
            {
                var forMonth = new DateTime(nextMonth.Year, nextMonth.Month, 1);
                var exists = await _context.Forecasts
                    .AnyAsync(f => f.UserId == userId && f.ForMonth == forMonth);
                if (!exists)
                {
                    _context.Forecasts.Add(new Forecast
                    {
                        UserId = userId,
                        ForMonth = forMonth,
                        PredictedIncome = avgIncome,
                        PredictedExpense = avgExpense,
                        ConfidenceNote = "Based on last 3 months average",
                        GeneratedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
            catch { /* Forecast table shape may differ — ignore */ }

            return Ok(new {
                success = true,
                nextMonth = label,
                predictedIncome = Math.Round(avgIncome, 2),
                predictedExpense = Math.Round(avgExpense, 2),
                predictedNet = Math.Round(avgIncome - avgExpense, 2),
                basedOnMonths = months
            });
        }

        // ================= POST: /api/transactions/suggest-category =================
        /// <summary>Suggest a category from description using rules and past history.</summary>
        [HttpPost("suggest-category")]
        public async Task<IActionResult> SuggestCategory([FromBody] SuggestCategoryRequest model)
        {
            int userId = GetCurrentUserId();
            var text = (model?.Text ?? "").Trim().ToLowerInvariant();
            var type = (model?.Type ?? "Expense").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return Ok(new { success = true, category = (string?)null, categoryId = (int?)null, source = "none" });

            // 1) Learn from this user's past corrections / descriptions
            var past = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Type == type && t.Category != null
                            && t.Description != null && t.Description.Length > 2)
                .OrderByDescending(t => t.CreatedAt)
                .Take(200)
                .ToListAsync();

            foreach (var t in past)
            {
                var d = (t.Description ?? "").ToLowerInvariant();
                if (d.Length >= 3 && (text.Contains(d) || d.Contains(text)))
                {
                    return Ok(new
                    {
                        success = true,
                        category = t.Category!.Name,
                        categoryId = t.CategoryId,
                        source = "history"
                    });
                }
            }

            // 2) Keyword rules (student-relevant)
            var rules = new (string[] keys, string cat, string t)[]
            {
                (new[]{"allowance","pocket money","stipend","family"}, "Allowance", "Income"),
                (new[]{"salary","part time","part-time","job","shift","gig","freelance"}, "Part-time Job", "Income"),
                (new[]{"scholarship","grant","bursary"}, "Scholarship", "Income"),
                (new[]{"gift","present","birthday money"}, "Gift", "Income"),
                (new[]{"food","cafe","cafeteria","canteen","lunch","dinner","breakfast","restaurant","delivery","pizza","burger","coffee","chai","snack"}, "Food", "Expense"),
                (new[]{"uber","bus","metro","train","taxi","rickshaw","fuel","petrol","transport","ride"}, "Transport", "Expense"),
                (new[]{"hostel","rent","pg ","room rent","accommodation"}, "Hostel/Rent", "Expense"),
                (new[]{"book","tuition","stationery","exam","course","fee","library","notebook"}, "Academics", "Expense"),
                (new[]{"netflix","spotify","subscription","prime","youtube","premium","app store"}, "Subscriptions", "Expense"),
                (new[]{"movie","cinema","outing","party","game","concert","entertainment"}, "Entertainment", "Expense"),
            };

            foreach (var (keys, cat, t) in rules)
            {
                if (!string.Equals(t, type, StringComparison.OrdinalIgnoreCase)) continue;
                if (keys.Any(k => text.Contains(k)))
                {
                    // Compute outside EF expression tree (Split has optional args — not allowed in expression trees)
                    var catKey = cat.Contains('/') ? cat.Substring(0, cat.IndexOf('/')) : cat;
                    var catKeyLower = catKey.ToLowerInvariant();
                    var catLower = cat.ToLowerInvariant();
                    var typeNorm = type;

                    var candidates = await _context.Categories
                        .Where(c => (c.UserId == null || c.UserId == userId) && c.Type == typeNorm)
                        .ToListAsync();

                    var match = candidates
                        .Where(c => (c.Name ?? "").ToLowerInvariant().Contains(catKeyLower)
                                 || (c.Name ?? "").ToLowerInvariant() == catLower)
                        .OrderBy(c => c.UserId == null ? 1 : 0)
                        .FirstOrDefault();

                    if (match != null)
                        return Ok(new { success = true, category = match.Name, categoryId = match.CategoryId, source = "rules" });
                    return Ok(new { success = true, category = cat, categoryId = (int?)null, source = "rules" });
                }
            }

            return Ok(new { success = true, category = (string?)null, categoryId = (int?)null, source = "none" });
        }

        public class SuggestCategoryRequest
        {
            public string? Text { get; set; }
            public string? Type { get; set; } = "Expense";
        }


        // ================= POST: /api/transactions/import-csv =================
        /// <summary>
        /// Bulk import + optional batch category suggestions (SRS).
        /// CSV columns: Date, Description, Category, Amount [, Type]
        /// </summary>
        [HttpPost("import-csv")]
        public async Task<IActionResult> ImportCsv(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "Please upload a valid CSV file." });
            }

            int userId = GetCurrentUserId();
            int successRows = 0;
            int failedRows = 0;
            var batchSuggestions = new List<object>();

            // Preload user categories for suggestion matching
            var allCats = await _context.Categories
                .Where(c => c.UserId == null || c.UserId == userId)
                .ToListAsync();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string? header = await reader.ReadLineAsync();
                int rowNum = 1;
                while (!reader.EndOfStream)
                {
                    string? line = await reader.ReadLineAsync();
                    rowNum++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 4)
                    {
                        failedRows++;
                        continue;
                    }

                    try
                    {
                        DateTime date = DateTime.TryParse(parts[0].Trim(), out var parsedDate) ? parsedDate : DateTime.UtcNow.Date;
                        string desc = parts[1].Trim();
                        string catName = parts[2].Trim();
                        decimal amount = decimal.TryParse(parts[3].Trim(), out var parsedAmt) ? Math.Abs(parsedAmt) : 0m;
                        string type = parts.Length > 4 && parts[4].Trim().Equals("Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";

                        if (amount <= 0)
                        {
                            failedRows++;
                            continue;
                        }

                        var cat = allCats.FirstOrDefault(c =>
                            c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase) &&
                            (c.UserId == null || c.UserId == userId));

                        string? suggestedName = null;
                        int? suggestedId = null;
                        string suggestionSource = "csv";

                        if (cat == null)
                        {
                            // Suggest category from description keywords when CSV category is missing
                            var descLower = (desc ?? "").ToLowerInvariant();
                            var keywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["Food"] = new[] { "cafe", "cafeteria", "canteen", "restaurant", "food", "meal", "pizza", "burger", "lunch", "dinner", "breakfast", "snack", "zomato", "swiggy" },
                                ["Transport"] = new[] { "uber", "careem", "taxi", "bus", "metro", "fuel", "petrol", "rickshaw", "fare", "parking" },
                                ["Hostel/Rent"] = new[] { "rent", "hostel", "room", "pg", "accommodation" },
                                ["Academics"] = new[] { "book", "tuition", "fee", "stationery", "course", "exam", "library", "notebook" },
                                ["Subscriptions"] = new[] { "netflix", "spotify", "youtube", "subscription", "prime", "disney", "chatgpt" },
                                ["Entertainment"] = new[] { "movie", "cinema", "game", "outing", "concert", "party" },
                                ["Allowance"] = new[] { "allowance", "pocket money", "stipend" },
                                ["Part-time Job"] = new[] { "salary", "wage", "freelance", "gig", "part-time" },
                                ["Scholarship"] = new[] { "scholarship", "grant", "bursary" },
                                ["Gift"] = new[] { "gift", "present", "bonus" }
                            };

                            foreach (var kv in keywords)
                            {
                                if (kv.Value.Any(k => descLower.Contains(k)))
                                {
                                    var match = allCats.FirstOrDefault(c => c.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                                    if (match != null)
                                    {
                                        suggestedName = match.Name;
                                        suggestedId = match.CategoryId;
                                        suggestionSource = "batch-ai";
                                        cat = match;
                                        break;
                                    }
                                }
                            }

                            if (cat == null)
                            {
                                cat = new Category { Name = string.IsNullOrWhiteSpace(catName) ? (suggestedName ?? "Miscellaneous") : catName, Type = type, IsDefault = false, UserId = userId };
                                _context.Categories.Add(cat);
                                await _context.SaveChangesAsync();
                                allCats.Add(cat);
                            }
                            else if (!string.IsNullOrWhiteSpace(suggestedName))
                            {
                                batchSuggestions.Add(new
                                {
                                    row = rowNum,
                                    description = desc,
                                    originalCategory = catName,
                                    suggestedCategory = suggestedName,
                                    suggestedCategoryId = suggestedId,
                                    source = suggestionSource
                                });
                            }
                        }

                        _context.Transactions.Add(new Transaction
                        {
                            UserId = userId,
                            CategoryId = cat.CategoryId,
                            Amount = amount,
                            Type = type,
                            Description = desc,
                            Date = date,
                            CreatedAt = DateTime.UtcNow,
                            AiSuggestedCategoryId = suggestedId
                        });

                        successRows++;
                    }
                    catch
                    {
                        failedRows++;
                    }
                }
            }

            _context.CsvImportBatches.Add(new CsvImportBatch
            {
                UserId = userId,
                FileName = file.FileName,
                RowsTotal = successRows + failedRows,
                RowsImported = successRows,
                RowsFailed = failedRows,
                Status = "Completed",
                ImportedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"CSV Import finished: {successRows} transactions imported successfully, {failedRows} failed/skipped.",
                successRows,
                failedRows,
                batchSuggestions
            });
        }
    }
}
