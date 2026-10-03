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
    [Route("api/savings-goals")]
    public class SavingsGoalsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SavingsGoalsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // ================= GET: /api/savings-goals =================
        [HttpGet]
        public async Task<IActionResult> GetGoals()
        {
            int userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            decimal monthlySavingsRate = user?.MonthlySavingsGoal > 0 ? user.MonthlySavingsGoal : 150m;

            var goals = await _context.SavingsGoals
                .Where(g => g.UserId == userId)
                .OrderBy(g => g.IsAchieved)
                .ThenBy(g => g.GoalId)
                .ToListAsync();

            var result = goals.Select(g =>
            {
                decimal progress = g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100m : 0m;
                decimal remaining = Math.Max(0, g.TargetAmount - g.CurrentAmount);
                string? estDate = null;

                if (remaining > 0 && monthlySavingsRate > 0)
                {
                    double monthsNeeded = (double)(remaining / monthlySavingsRate);
                    estDate = DateTime.UtcNow.AddDays(monthsNeeded * 30.4).ToString("MMM yyyy");
                }
                else if (remaining <= 0)
                {
                    estDate = "Goal Reached!";
                }

                return new SavingsGoalDto
                {
                    GoalId = g.GoalId,
                    GoalName = g.GoalName,
                    TargetAmount = g.TargetAmount,
                    CurrentAmount = g.CurrentAmount,
                    ProgressPercentage = Math.Min(100, Math.Round(progress, 1)),
                    TargetDate = g.TargetDate?.ToString("yyyy-MM-dd"),
                    EstimatedCompletionDate = estDate,
                    IsAchieved = g.IsAchieved || g.CurrentAmount >= g.TargetAmount
                };
            }).ToList();

            return Ok(result);
        }

        // ================= POST: /api/savings-goals =================
        [HttpPost]
        public async Task<IActionResult> CreateGoal([FromBody] CreateSavingsGoalDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Please provide valid goal information." });
            }

            int userId = GetCurrentUserId();
            var goal = new SavingsGoal
            {
                UserId = userId,
                GoalName = model.GoalName.Trim(),
                TargetAmount = Math.Abs(model.TargetAmount),
                CurrentAmount = Math.Abs(model.CurrentAmount),
                TargetDate = model.TargetDate,
                IsAchieved = model.CurrentAmount >= model.TargetAmount,
                CreatedAt = DateTime.UtcNow
            };

            _context.SavingsGoals.Add(goal);
            await _context.SaveChangesAsync();

            return Ok(new SavingsGoalDto
            {
                GoalId = goal.GoalId,
                GoalName = goal.GoalName,
                TargetAmount = goal.TargetAmount,
                CurrentAmount = goal.CurrentAmount,
                ProgressPercentage = goal.TargetAmount > 0 ? Math.Round((goal.CurrentAmount / goal.TargetAmount) * 100m, 1) : 0,
                TargetDate = goal.TargetDate?.ToString("yyyy-MM-dd"),
                IsAchieved = goal.IsAchieved
            });
        }

        // ================= PUT: /api/savings-goals/{id} =================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGoal(int id, [FromBody] CreateSavingsGoalDto model)
        {
            int userId = GetCurrentUserId();
            var goal = await _context.SavingsGoals.FirstOrDefaultAsync(g => g.GoalId == id && g.UserId == userId);

            if (goal == null) return NotFound(new { success = false, message = "Goal not found." });

            goal.GoalName = model.GoalName.Trim();
            goal.TargetAmount = Math.Abs(model.TargetAmount);
            goal.CurrentAmount = Math.Abs(model.CurrentAmount);
            goal.TargetDate = model.TargetDate;
            goal.IsAchieved = goal.CurrentAmount >= goal.TargetAmount;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Goal updated successfully." });
        }

        // ================= POST: /api/savings-goals/{id}/contribute =================
        [HttpPost("{id}/contribute")]
        public async Task<IActionResult> Contribute(int id, [FromBody] ContributeGoalDto model)
        {
            if (!ModelState.IsValid || model.Amount <= 0)
            {
                return BadRequest(new { success = false, message = "Please provide a valid contribution amount." });
            }

            int userId = GetCurrentUserId();
            var goal = await _context.SavingsGoals.FirstOrDefaultAsync(g => g.GoalId == id && g.UserId == userId);

            if (goal == null) return NotFound(new { success = false, message = "Goal not found." });

            goal.CurrentAmount += model.Amount;
            if (goal.CurrentAmount >= goal.TargetAmount && !goal.IsAchieved)
            {
                goal.IsAchieved = true;
                _context.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Message = $"🎉 Congratulations! You reached your savings goal: '{goal.GoalName}' (${goal.TargetAmount:F2})!",
                    Type = "Info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                currentAmount = goal.CurrentAmount,
                progressPercentage = Math.Min(100, Math.Round((goal.CurrentAmount / goal.TargetAmount) * 100m, 1)),
                isAchieved = goal.IsAchieved
            });
        }

        // ================= DELETE: /api/savings-goals/{id} =================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            int userId = GetCurrentUserId();
            var goal = await _context.SavingsGoals.FirstOrDefaultAsync(g => g.GoalId == id && g.UserId == userId);

            if (goal == null) return NotFound(new { success = false, message = "Goal not found." });

            _context.SavingsGoals.Remove(goal);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Goal removed." });
        }
    }
}
