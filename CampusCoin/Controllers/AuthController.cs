// SOLUTION PATH: Controllers/AuthController.cs  (REPLACES your existing file)
using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Models.DTOs;
using CampusCoin.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoin.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<AuthController> _logger;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            ILogger<AuthController> logger,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
        }

        // ================= GET: /Auth/Login (Student + toggle) =================
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Student login uses frontend Account UI
            if (!string.IsNullOrEmpty(returnUrl))
                return RedirectToAction("Login", "Account", new { returnUrl });
            return RedirectToAction("Index", "Home");
        }

        // ================= GET: /Auth/AdminLogin (SEPARATE, DIRECT-ACCESS ADMIN ENTRY) =================
        // SRS 1.6: "Student registration and login; separate, direct-access administrator login"
        [HttpGet]
        public async Task<IActionResult> AdminLogin()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                if (role == "Admin" || role == "DemoAdmin") return RedirectToAction("Index", "Admin");
                // A signed-in non-admin hitting the admin portal gets signed out first,
                // so their student session can never carry over into the admin area.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
            return View(); // Views/Auth/AdminLogin.cshtml
        }

        // ================= POST: /Auth/Login =================
        // portal="admin" is sent ONLY by the AdminLogin page. It forces role
        // enforcement server-side so a student account cannot use this door.
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest model, [FromQuery] string? portal = null)
        {
            try
            {
                return await LoginCoreAsync(model, portal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed with an unexpected server error.");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Server error while signing in: " + ex.GetBaseException().Message
                });
            }
        }

        private async Task<IActionResult> LoginCoreAsync(LoginRequest model, string? portal)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Please provide both email and password." });
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.Trim().ToLower());

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { success = false, message = "Invalid email or account is inactive." });
            }

            bool isPasswordValid = false;

            try
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
                if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    isPasswordValid = true;
                }
            }
            catch
            {
                // Fallback for custom or legacy seeded hashes
            }

            // Legacy plaintext fallbacks removed for public/portfolio security.
            // All accounts must use properly hashed passwords (seeded via DbInitializer).

            if (!isPasswordValid)
            {
                return Unauthorized(new { success = false, message = "Incorrect password." });
            }

            var roleName = user.Role?.RoleName ?? "Student";

            // ---- SEPARATE ADMIN PORTAL ENFORCEMENT ----
            // Full Admin and read-only DemoAdmin may use the admin portal.
            if (string.Equals(portal, "admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(roleName, "DemoAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "This portal is for administrators only. Please use the student login."
                });
            }
            // --------------------------------------------

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleName)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            try
            {
                _context.ChangeTracker.Clear();
                _context.UserSessions.Add(new UserSession
                {
                    UserId = user.UserId,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    ExpiresAt = DateTime.UtcNow.AddHours(12),
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not write UserSessions row for user {UserId}.", user.UserId);
            }

            string redirectUrl = (roleName == "Admin" || roleName == "DemoAdmin") ? "/Admin" : "/Dashboard/Index";
            return Ok(new { success = true, role = roleName, redirectUrl });
        }

        // ================= GET: /Auth/Register =================
        [HttpGet]
        public IActionResult Register()
        {
            // Student registration uses frontend Account UI
            return RedirectToAction("Register", "Account");
        }

        // ================= POST: /Auth/Register =================
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Please fill in all required registration fields correctly." });
            }

            string emailNorm = model.Email.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailNorm))
            {
                return BadRequest(new { success = false, message = "A user account with this email address already exists." });
            }

            var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Student");
            if (studentRole == null)
            {
                studentRole = new Role { RoleName = "Student" };
                _context.Roles.Add(studentRole);
                await _context.SaveChangesAsync();
            }

            var newUser = new User
            {
                FullName = model.FullName.Trim(),
                Email = emailNorm,
                AcademicYear = model.AcademicYear ?? "1st Year",
                MonthlyAllowanceBaseline = model.MonthlyAllowanceBaseline > 0 ? model.MonthlyAllowanceBaseline : 1000m,
                MonthlySavingsGoal = model.MonthlySavingsGoal > 0 ? model.MonthlySavingsGoal : 200m,
                RoleId = studentRole.RoleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            newUser.PasswordHash = _passwordHasher.HashPassword(newUser, model.Password);
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _context.SavingsGoals.Add(new SavingsGoal
            {
                UserId = newUser.UserId,
                GoalName = "Emergency Buffer",
                TargetAmount = 1000m,
                CurrentAmount = 0m,
                TargetDate = DateTime.UtcNow.AddMonths(4),
                CreatedAt = DateTime.UtcNow
            });

            _context.Notifications.Add(new Notification
            {
                UserId = newUser.UserId,
                Message = $"Welcome to CampusCoin, {newUser.FullName}! Start by logging your first expense or setting a budget.",
                Type = "Info",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Account is created; user signs in separately
            return Ok(new
            {
                success = true,
                message = "Account created successfully! Please sign in with your email and password.",
                redirectUrl = "/Account/Login"
            });
        }

        // ================= GET/POST: /Auth/Logout =================
        [HttpGet, HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ================= GET: /Auth/ForgotPassword =================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        // ================= POST: /Auth/ForgotPassword =================
        [HttpPost]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Email))
            {
                return BadRequest(new { success = false, message = "Please enter a valid email." });
            }

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            // Always return a generic success message (do not leak whether the email exists)
            const string genericMsg = "If an account exists for that email, a password reset link has been sent. Please check your inbox (and spam folder).";

            if (user == null)
            {
                return Ok(new { success = true, message = genericMsg });
            }

            var token = Guid.NewGuid().ToString("N");
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.UserId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(2),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Absolute URL so the link works from the email client
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var resetUrl = $"{baseUrl}/Auth/ResetPassword?token={token}";

            var displayName = string.IsNullOrWhiteSpace(user.FullName) ? "there" : user.FullName.Split(' ')[0];
            var html = $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:16px;"">
  <div style=""background:linear-gradient(135deg,#0ea5e9,#6366f1);padding:20px 24px;border-radius:12px 12px 0 0;color:#fff;"">
    <h2 style=""margin:0;font-size:20px;"">Campus Coin — Password Reset</h2>
  </div>
  <div style=""background:#fff;padding:24px;border-radius:0 0 12px 12px;border:1px solid #e2e8f0;"">
    <p style=""margin:0 0 12px;color:#0f172a;"">Hi {System.Net.WebUtility.HtmlEncode(displayName)},</p>
    <p style=""margin:0 0 16px;color:#334155;line-height:1.55;"">We received a request to reset your Campus Coin password. Click the button below to choose a new one. This link expires in <strong>2 hours</strong> and can only be used once.</p>
    <p style=""text-align:center;margin:28px 0;"">
      <a href=""{resetUrl}"" style=""display:inline-block;padding:14px 28px;background:linear-gradient(135deg,#0ea5e9,#6366f1);color:#fff;text-decoration:none;border-radius:999px;font-weight:700;font-size:14px;"">Reset my password</a>
    </p>
    <p style=""margin:0 0 8px;color:#64748b;font-size:13px;"">Or copy and paste this link into your browser:</p>
    <p style=""margin:0 0 20px;word-break:break-all;font-size:12px;color:#475569;"">{System.Net.WebUtility.HtmlEncode(resetUrl)}</p>
    <p style=""margin:0;color:#94a3b8;font-size:12px;"">If you did not request this, you can safely ignore this email. Your password will stay the same.</p>
  </div>
</div>";

            var (sent, err) = await _emailService.SendAsync(user.Email, "Reset your Campus Coin password", html);
            if (!sent)
            {
                _logger.LogError("Password reset email failed for {Email}: {Err}", user.Email, err);
                // Still return success message for security, but log the failure
                return Ok(new { success = true, message = genericMsg, emailSent = false, detail = "Email could not be sent. Please try again later or contact support." });
            }

            return Ok(new { success = true, message = genericMsg, emailSent = true });
        }

        // ================= GET: /Auth/ResetPassword =================
        [HttpGet]
        public IActionResult ResetPassword(string? token = null)
        {
            ViewBag.Token = token;
            return View();
        }

        // ================= POST: /Auth/ResetPassword =================
        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid reset request data." });
            }

            var resetToken = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == model.Token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

            if (resetToken == null || resetToken.User == null)
            {
                return BadRequest(new { success = false, message = "Password reset link is invalid or has expired." });
            }

            resetToken.User.PasswordHash = _passwordHasher.HashPassword(resetToken.User, model.NewPassword);
            resetToken.IsUsed = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Your password has been successfully reset! You can now log in." });
        }

        // ================= Profile API Endpoints =================
        [Authorize]
        [HttpGet("/api/auth/me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return NotFound();

            return Ok(new UserProfileResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role?.RoleName ?? "Student",
                AcademicYear = user.AcademicYear,
                MonthlyAllowanceBaseline = user.MonthlyAllowanceBaseline,
                MonthlySavingsGoal = user.MonthlySavingsGoal
            });
        }

        [Authorize]
        [HttpPut("/api/auth/profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid profile data." });
            }

            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.FullName = model.FullName.Trim();
            user.AcademicYear = model.AcademicYear?.Trim();
            user.MonthlyAllowanceBaseline = model.MonthlyAllowanceBaseline;
            user.MonthlySavingsGoal = model.MonthlySavingsGoal;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Profile updated successfully." });
        }


        [Authorize]
        [HttpPost("/api/auth/change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
            {
                return BadRequest(new { success = false, message = "Current and new password are required." });
            }
            if (model.NewPassword.Length < 6)
            {
                return BadRequest(new { success = false, message = "New password must be at least 6 characters." });
            }

            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { success = false, message = "User not found." });

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
            if (verify == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { success = false, message = "Current password is incorrect." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Password updated successfully." });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }
    }
}
