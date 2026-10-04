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
    [Route("api/bookmarks")]
    public class BookmarksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BookmarksController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized();

                var bookmarks = await _context.Bookmarks
                    .Where(b => b.UserId == userId)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();

                var result = new List<object>();

                foreach (var bm in bookmarks)
                {
                    string title = bm.Title ?? "";
                    string content = bm.Content ?? "";
                    string category = bm.Category ?? "Saved";
                    string refType = bm.RefType ?? "Tip";

                    // Prefer live content when linked to DB tip/insight
                    try
                    {
                        if (string.Equals(refType, "Tip", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(refType, "SmartTip", StringComparison.OrdinalIgnoreCase))
                        {
                            var tip = await _context.SavingTips.FindAsync(bm.RefId);
                            if (tip != null)
                            {
                                if (string.IsNullOrWhiteSpace(title))
                                    title = tip.TriggerCategory != null ? $"{tip.TriggerCategory} Saving Tip" : "Saving Tip";
                                if (string.IsNullOrWhiteSpace(content))
                                    content = tip.TipText;
                                if (string.IsNullOrWhiteSpace(category) || category == "Saved")
                                    category = "Saving Tip";
                            }
                        }
                        else if (string.Equals(refType, "Insight", StringComparison.OrdinalIgnoreCase))
                        {
                            var insight = await _context.Insights.FindAsync(bm.RefId);
                            if (insight != null)
                            {
                                if (string.IsNullOrWhiteSpace(title))
                                    title = "Monthly Insight";
                                if (string.IsNullOrWhiteSpace(content))
                                    content = insight.SummaryText ?? insight.TipText ?? "";
                                if (string.IsNullOrWhiteSpace(category) || category == "Saved")
                                    category = "AI Insight";
                            }
                        }
                    }
                    catch { /* use snapshot fields */ }

                    // Snapshot-only (smart tips with hash ids)
                    if (string.IsNullOrWhiteSpace(title))
                        title = "Saved tip";
                    if (string.IsNullOrWhiteSpace(content))
                        content = "Open Dashboard to see current tips.";

                    result.Add(new
                    {
                        bookmarkId = bm.BookmarkId,
                        refType,
                        refId = bm.RefId,
                        title,
                        content,
                        category,
                        createdAt = bm.CreatedAt.ToString("MMM dd, yyyy")
                    });
                }

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Failed to load bookmarks" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add([FromBody] BookmarkCreateDto? model)
        {
            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0 || User.Identity?.IsAuthenticated != true)
                    return Unauthorized();

                if (model == null || string.IsNullOrWhiteSpace(model.RefType))
                    return BadRequest(new { message = "Invalid data." });
                if (model.RefType.Trim().Length > 50 || model.RefId < 0)
                    return BadRequest(new { message = "Invalid data." });

                var existing = await _context.Bookmarks
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.RefType == model.RefType && b.RefId == model.RefId);

                if (existing != null)
                {
                    // Refresh snapshot if provided
                    if (!string.IsNullOrWhiteSpace(model.Title)) existing.Title = model.Title.Trim();
                    if (!string.IsNullOrWhiteSpace(model.Content)) existing.Content = model.Content.Trim();
                    if (!string.IsNullOrWhiteSpace(model.Category)) existing.Category = model.Category.Trim();
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Already bookmarked.", alreadyExists = true, bookmarkId = existing.BookmarkId });
                }

                var bookmark = new Bookmark
                {
                    UserId = userId,
                    RefType = model.RefType.Trim(),
                    RefId = model.RefId,
                    Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title.Trim(),
                    Content = string.IsNullOrWhiteSpace(model.Content) ? null : model.Content.Trim(),
                    Category = string.IsNullOrWhiteSpace(model.Category) ? "Saving Tip" : model.Category.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Bookmarks.Add(bookmark);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Bookmarked successfully!", bookmarkId = bookmark.BookmarkId });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Failed to save bookmark" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = GetCurrentUserId();
            var bm = await _context.Bookmarks
                .FirstOrDefaultAsync(b => b.BookmarkId == id && b.UserId == userId);

            if (bm == null) return NotFound(new { message = "Not found." });

            _context.Bookmarks.Remove(bm);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Removed." });
        }
    }

    public class BookmarkCreateDto
    {
        public string RefType { get; set; } = "Tip";
        public int RefId { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Category { get; set; }
    }
}
