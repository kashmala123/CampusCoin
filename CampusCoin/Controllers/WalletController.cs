using CampusCoin.Data;
using CampusCoin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CampusCoin.Controllers
{
    [Authorize(Roles = "Student")]
    public class WalletController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public WalletController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        /// <summary>Auth must succeed before any user-controlled input is trusted.</summary>
        private bool TryGetAuthenticatedUserId(out int userId)
        {
            userId = GetCurrentUserId();
            return userId > 0 && User.Identity?.IsAuthenticated == true;
        }

        private static string AccountCode(int userId) => $"CC-{userId:D4}-PK";


        /// <summary>
        /// Store wallet PIN using ASP.NET Identity <see cref="IPasswordHasher{TUser}"/> (full hash, never truncated).
        /// Legacy formats (plaintext 4-digit, previous truncated SHA256) are verified once and upgraded in place.
        /// </summary>
        private string HashNewWalletPin(User user, string pin) =>
            _passwordHasher.HashPassword(user, pin.Trim());

        /// <summary>
        /// Legacy truncated-SHA256 format from an earlier CodeQL fix (20 hex chars).
        /// Used only to verify and upgrade — never for new storage.
        /// </summary>
        private static string LegacyTruncatedSha256Pin(int userId, string pin)
        {
            var material = Encoding.UTF8.GetBytes($"CampusCoin|WalletPin|{userId}|{pin.Trim()}");
            var hash = SHA256.HashData(material);
            return Convert.ToHexString(hash).Substring(0, 20);
        }

        private static bool LooksLikeLegacyPlaintextPin(string stored) =>
            stored.Length <= 6 && stored.All(char.IsDigit);

        private static bool LooksLikeLegacyTruncatedSha256(string stored) =>
            stored.Length == 20 && stored.All(c => Uri.IsHexDigit(c));

        /// <summary>
        /// Verify submitted PIN against stored value. When a legacy format matches, <paramref name="upgradedHash"/>
        /// is set so the caller can persist the Identity password-hash upgrade.
        /// </summary>
        private bool TryVerifyWalletPin(User user, string provided, out string? upgradedHash)
        {
            upgradedHash = null;
            if (string.IsNullOrWhiteSpace(provided))
                return false;

            var pin = provided.Trim();
            var stored = user.WalletPin;

            // First-time: no PIN stored yet — caller will hash and save
            if (string.IsNullOrEmpty(stored))
                return true;

            // Legacy plaintext (4-digit)
            if (LooksLikeLegacyPlaintextPin(stored))
            {
                if (stored != pin)
                    return false;
                upgradedHash = HashNewWalletPin(user, pin);
                return true;
            }

            // Legacy truncated SHA256 (previous CodeQL interim format)
            if (LooksLikeLegacyTruncatedSha256(stored))
            {
                if (!string.Equals(stored, LegacyTruncatedSha256Pin(user.UserId, pin), StringComparison.Ordinal))
                    return false;
                upgradedHash = HashNewWalletPin(user, pin);
                return true;
            }

            // Current: ASP.NET Identity password hash
            var result = _passwordHasher.VerifyHashedPassword(user, stored, pin);
            if (result == PasswordVerificationResult.Failed)
                return false;

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
                upgradedHash = HashNewWalletPin(user, pin);

            return true;
        }



        [HttpGet("/Dashboard/Wallet")]
        public IActionResult Index() => View("~/Views/Dashboard/Wallet.cshtml");

        [HttpGet("/api/wallet/summary")]
        public async Task<IActionResult> GetSummary()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var accountId = AccountCode(userId);
            var balance = user.MonthlyAllowanceBaseline;

            var pendingFees = await _context.FeeVouchers
                .Where(v => v.UserId == userId && v.Status != "Paid")
                .OrderBy(v => v.DueDate)
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    v.VoucherCode,
                    amount = v.Amount + v.Fine - v.Discount,
                    v.DueDate,
                    v.Status
                })
                .ToListAsync();

            var qrPayload = $"CAMPUSCOIN|WALLET|{accountId}|{user.FullName}";

            return Ok(new
            {
                balance,
                accountId,
                qrPayload,
                userName = user.FullName,
                academicYear = user.AcademicYear ?? "Student",
                email = user.Email,
                pendingFees,
                lowBalance = balance < 2000m
            });
        }

        [HttpGet("/api/wallet/transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            var ledgers = await _context.WalletLedgers
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .ThenByDescending(l => l.LedgerId)
                .Take(50)
                .ToListAsync();

            if (ledgers.Count > 0)
            {
                var result = ledgers.Select(l => new
                {
                    id = l.ReferenceCode ?? $"WL-{l.LedgerId}",
                    ledgerId = l.LedgerId,
                    title = l.Description ?? l.EntryType,
                    category = MapCategory(l.EntryType),
                    type = l.Direction == "Credit" ? "Incoming" : "Outgoing",
                    amount = l.Amount,
                    date = l.CreatedAt.ToString("MMM dd, yyyy • HH:mm"),
                    status = "Completed",
                    method = l.Method,
                    balanceAfter = l.BalanceAfter
                });
                return Ok(result);
            }

            var txns = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.TransactionId)
                .Take(50)
                .ToListAsync();

            return Ok(txns.Select(t =>
            {
                var desc = t.Description ?? t.Category?.Name ?? "Transaction";
                return new
                {
                    id = $"TX-{t.TransactionId}",
                    ledgerId = 0,
                    title = desc,
                    category = GuessCategory(desc),
                    type = t.Type == "Income" ? "Incoming" : "Outgoing",
                    amount = t.Amount,
                    date = t.Date.ToString("MMM dd, yyyy • HH:mm"),
                    status = "Completed",
                    method = (string?)null,
                    balanceAfter = (decimal?)null
                };
            }));
        }

        
        // ================= GET: /api/wallet/lookup?q= =================
        // Real student search for peer transfer (email, name, CC-XXXX-PK)
        [HttpGet("/api/wallet/lookup")]
        public async Task<IActionResult> LookupStudents([FromQuery] string? q)
        {
            int userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized();

            q = (q ?? "").Trim();
            if (q.Length < 2)
                return Ok(Array.Empty<object>());

            var query = _context.Users
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.UserId != userId && u.Role != null && u.Role.RoleName == "Student");

            if (q.StartsWith("CC-", StringComparison.OrdinalIgnoreCase))
            {
                var parts = q.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int rid))
                    query = query.Where(u => u.UserId == rid);
                else
                    query = query.Where(u => false);
            }
            else
            {
                var term = q.ToLower();
                query = query.Where(u =>
                    u.Email.ToLower().Contains(term) ||
                    u.FullName.ToLower().Contains(term));
            }

            var list = await query
                .OrderBy(u => u.FullName)
                .Take(8)
                .Select(u => new
                {
                    userId = u.UserId,
                    fullName = u.FullName,
                    email = u.Email,
                    accountId = $"CC-{u.UserId:D4}-PK",
                    academicYear = u.AcademicYear
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("/api/wallet/topup")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUp([FromBody] WalletTopUpDto? model)
        {
            try
            {
                if (!TryGetAuthenticatedUserId(out int userId))
                    return Unauthorized(new { success = false, message = "Not logged in." });

                if (model == null || model.Amount <= 0)
                    return BadRequest(new { success = false, message = "Enter a valid amount." });
                if (model.Amount > 500000)
                    return BadRequest(new { success = false, message = "Maximum top-up is PKR 500,000." });

                var user = await _context.Users.FindAsync(userId);
                if (user == null) return NotFound(new { success = false, message = "User not found." });

                var method = string.IsNullOrWhiteSpace(model.Method) ? "Online" : model.Method.Trim();
                user.MonthlyAllowanceBaseline += model.Amount;
                var balanceAfter = user.MonthlyAllowanceBaseline;
                var refCode = $"TOP-{DateTime.UtcNow:yyyyMMddHHmmss}-{userId}";

                var incomeCat = await GetOrCreateCategory(userId, "Wallet Top-Up", "Income");
                var txn = new Transaction
                {
                    UserId = userId,
                    CategoryId = incomeCat.CategoryId,
                    Amount = model.Amount,
                    Type = "Income",
                    Description = $"Wallet Top-Up via {method}",
                    Date = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(txn);
                await _context.SaveChangesAsync();

                _context.WalletLedgers.Add(new WalletLedger
                {
                    UserId = userId,
                    EntryType = "TopUp",
                    Direction = "Credit",
                    Amount = model.Amount,
                    BalanceAfter = balanceAfter,
                    Method = method,
                    Description = $"Wallet Top-Up via {method}",
                    ReferenceCode = refCode,
                    RelatedTransactionId = txn.TransactionId,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"PKR {model.Amount:N0} added via {method}.",
                    balance = balanceAfter,
                    referenceCode = refCode
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Wallet operation failed. Please try again." });
            }
        }

        [HttpPost("/api/wallet/transfer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Transfer([FromBody] WalletTransferDto? model)
        {
            try
            {
                if (!TryGetAuthenticatedUserId(out int userId))
                    return Unauthorized(new { success = false, message = "Not logged in." });

                if (model == null || model.Amount < 50)
                    return BadRequest(new { success = false, message = "Minimum transfer is PKR 50." });
                if (model.Amount > 500000)
                    return BadRequest(new { success = false, message = "Maximum transfer is PKR 500,000." });

                var sender = await _context.Users.FindAsync(userId);
                if (sender == null) return NotFound(new { success = false, message = "Sender not found." });

                if (sender.MonthlyAllowanceBaseline < model.Amount)
                    return BadRequest(new { success = false, message = "Insufficient wallet balance." });

                var target = (model.Recipient ?? "").Trim();
                if (string.IsNullOrWhiteSpace(target) || target.Length > 150)
                    return BadRequest(new { success = false, message = "Enter recipient email, name, or account ID (CC-XXXX-PK)." });

                if (string.IsNullOrWhiteSpace(model.Pin) || model.Pin.Trim().Length < 4 || model.Pin.Trim().Length > 12)
                    return BadRequest(new { success = false, message = "Enter your 4-digit wallet PIN." });

                var pin = model.Pin.Trim();
                // Keep 4-digit business rule when setting/using PIN (digits only)
                if (pin.Length == 4 && !pin.All(char.IsDigit))
                    return BadRequest(new { success = false, message = "Enter your 4-digit wallet PIN." });

                if (string.IsNullOrEmpty(sender.WalletPin))
                {
                    // First-time PIN setup — store Identity password hash only (never plaintext)
                    sender.WalletPin = HashNewWalletPin(sender, pin);
                }
                else if (!TryVerifyWalletPin(sender, pin, out var upgradedHash))
                {
                    return BadRequest(new { success = false, message = "Invalid wallet PIN." });
                }
                else if (upgradedHash != null)
                {
                    // Legacy plaintext / truncated-SHA256 / rehash-needed → upgrade in place
                    sender.WalletPin = upgradedHash;
                }

                User? recipient = null;
                if (target.StartsWith("CC-", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = target.Split('-', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int rid))
                        recipient = await _context.Users.FirstOrDefaultAsync(u => u.UserId == rid && u.IsActive);
                }
                if (recipient == null && int.TryParse(target, out int uidOnly))
                {
                    recipient = await _context.Users.FirstOrDefaultAsync(u => u.UserId == uidOnly && u.IsActive);
                }
                if (recipient == null)
                {
                    var term = target.ToLower();
                    recipient = await _context.Users
                        .Include(u => u.Role)
                        .Where(u => u.IsActive && u.UserId != userId)
                        .FirstOrDefaultAsync(u =>
                            u.Email.ToLower() == term ||
                            u.Email.ToLower().Contains(term) ||
                            u.FullName.ToLower() == term ||
                            u.FullName.ToLower().Contains(term));
                }

                if (recipient == null)
                    return BadRequest(new { success = false, message = "Recipient not found. Try their email or CC-XXXX-PK from their Wallet page." });
                if (recipient.UserId == userId)
                    return BadRequest(new { success = false, message = "You cannot transfer to yourself." });

                sender.MonthlyAllowanceBaseline -= model.Amount;
                recipient.MonthlyAllowanceBaseline += model.Amount;

                var note = string.IsNullOrWhiteSpace(model.Note) ? "" : $" — {model.Note.Trim()}";
                var refCode = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{userId}";

                var expenseCat = await GetOrCreateCategory(userId, "Transfer", "Expense");
                var incomeCat = await GetOrCreateCategory(recipient.UserId, "Transfer", "Income");

                var outTxn = new Transaction
                {
                    UserId = userId,
                    CategoryId = expenseCat.CategoryId,
                    Amount = model.Amount,
                    Type = "Expense",
                    Description = $"Transfer to {recipient.FullName}{note}",
                    Date = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow
                };
                var inTxn = new Transaction
                {
                    UserId = recipient.UserId,
                    CategoryId = incomeCat.CategoryId,
                    Amount = model.Amount,
                    Type = "Income",
                    Description = $"Transfer from {sender.FullName}{note}",
                    Date = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(outTxn);
                _context.Transactions.Add(inTxn);
                await _context.SaveChangesAsync();

                _context.WalletLedgers.Add(new WalletLedger
                {
                    UserId = userId,
                    EntryType = "TransferOut",
                    Direction = "Debit",
                    Amount = model.Amount,
                    BalanceAfter = sender.MonthlyAllowanceBaseline,
                    Method = "Peer Transfer",
                    CounterpartyAccount = AccountCode(recipient.UserId),
                    CounterpartyName = recipient.FullName,
                    Description = $"Transfer to {recipient.FullName}{note}",
                    ReferenceCode = refCode,
                    RelatedUserId = recipient.UserId,
                    RelatedTransactionId = outTxn.TransactionId,
                    CreatedAt = DateTime.UtcNow
                });
                _context.WalletLedgers.Add(new WalletLedger
                {
                    UserId = recipient.UserId,
                    EntryType = "TransferIn",
                    Direction = "Credit",
                    Amount = model.Amount,
                    BalanceAfter = recipient.MonthlyAllowanceBaseline,
                    Method = "Peer Transfer",
                    CounterpartyAccount = AccountCode(userId),
                    CounterpartyName = sender.FullName,
                    Description = $"Transfer from {sender.FullName}{note}",
                    ReferenceCode = refCode + "-IN",
                    RelatedUserId = userId,
                    RelatedTransactionId = inTxn.TransactionId,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Transferred PKR {model.Amount:N0} to {recipient.FullName}.",
                    balance = sender.MonthlyAllowanceBaseline,
                    referenceCode = refCode
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Wallet operation failed. Please try again." });
            }
        }

        [HttpPost("/api/wallet/pay-fee/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayFee(int id)
        {
            try
            {
                if (!TryGetAuthenticatedUserId(out int userId))
                    return Unauthorized(new { success = false, message = "Not logged in." });
                if (id <= 0)
                    return BadRequest(new { success = false, message = "Invalid voucher." });

                var voucher = await _context.FeeVouchers
                    .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

                if (voucher == null)
                    return NotFound(new { success = false, message = "Voucher not found." });
                if (voucher.Status == "Paid")
                    return BadRequest(new { success = false, message = "Already paid." });

                var total = voucher.Amount + voucher.Fine - voucher.Discount;
                if (total < 0) total = 0;

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "User not found." });
                if (user.MonthlyAllowanceBaseline < total)
                    return BadRequest(new { success = false, message = "Insufficient wallet balance." });

                user.MonthlyAllowanceBaseline -= total;
                voucher.Status = "Paid";
                voucher.Fine = 0;
                voucher.PaidAt = DateTime.UtcNow;
                voucher.PaymentMethod = "Wallet";
                voucher.TransactionRef = $"WAL-{DateTime.UtcNow:yyyyMMddHHmmss}";

                var feeCat = await GetOrCreateCategory(userId, "University Fees", "Expense");
                var txn = new Transaction
                {
                    UserId = userId,
                    CategoryId = feeCat.CategoryId,
                    Amount = total,
                    Type = "Expense",
                    Description = $"Fee: {voucher.Title}",
                    Date = DateTime.UtcNow.Date,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Transactions.Add(txn);
                await _context.SaveChangesAsync();

                _context.WalletLedgers.Add(new WalletLedger
                {
                    UserId = userId,
                    EntryType = "FeePay",
                    Direction = "Debit",
                    Amount = total,
                    BalanceAfter = user.MonthlyAllowanceBaseline,
                    Method = "Wallet",
                    Description = $"Fee: {voucher.Title}",
                    ReferenceCode = voucher.TransactionRef,
                    RelatedTransactionId = txn.TransactionId,
                    RelatedFeeVoucherId = voucher.Id,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Fee paid from wallet successfully.",
                    balance = user.MonthlyAllowanceBaseline,
                    transactionRef = voucher.TransactionRef
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Wallet operation failed. Please try again." });
            }
        }

        private async Task<Category> GetOrCreateCategory(int userId, string name, string type)
        {
            var cat = await _context.Categories
                .FirstOrDefaultAsync(c =>
                    c.Type == type &&
                    (c.UserId == null || c.UserId == userId) &&
                    c.Name == name);

            if (cat != null) return cat;

            cat = new Category
            {
                Name = name,
                Type = type,
                IsDefault = false,
                UserId = userId
            };
            _context.Categories.Add(cat);
            await _context.SaveChangesAsync();
            return cat;
        }

        private static string MapCategory(string entryType) => entryType switch
        {
            "TopUp" => "Top-Up",
            "TransferIn" or "TransferOut" => "Transfer",
            "FeePay" => "Fees",
            "EventPay" => "Events",
            _ => "Wallet"
        };

        private static string GuessCategory(string desc)
        {
            if (desc.Contains("Top-Up", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("Top Up", StringComparison.OrdinalIgnoreCase)) return "Top-Up";
            if (desc.Contains("Transfer", StringComparison.OrdinalIgnoreCase)) return "Transfer";
            if (desc.Contains("Fee", StringComparison.OrdinalIgnoreCase)) return "Fees";
            if (desc.Contains("Event", StringComparison.OrdinalIgnoreCase)) return "Events";
            return "General";
        }
    }

    public class WalletTopUpDto
    {
        public decimal Amount { get; set; }
        public string? Method { get; set; }
    }

    public class WalletTransferDto
    {
        public string? Recipient { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
        public string? Pin { get; set; }
    }
}
