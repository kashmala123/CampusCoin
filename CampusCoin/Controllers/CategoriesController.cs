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
    [Route("api/categories")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out int id) ? id : 0;
        }

        // GET: /api/categories
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            int userId = GetCurrentUserId();

            var categories = await _context.Categories
                .Where(c => c.UserId == null || c.UserId == userId)
                .ToListAsync();

            var result = categories.Select(c => new
            {
                categoryId = c.CategoryId,
                name = c.Name,
                type = c.Type,
                isDefault = c.IsDefault,
                isUserCustom = c.UserId == userId && !c.IsDefault
            });

            return Ok(result);
        }

        // POST: /api/categories
        [HttpPost]
        public async Task<IActionResult> AddCategory([FromBody] CreateCategoryDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { message = "Category name is required." });

            if (model.Type != "Income" && model.Type != "Expense")
                return BadRequest(new { message = "Type must be 'Income' or 'Expense'." });

            int userId = GetCurrentUserId();

            // Check for duplicate
            var exists = await _context.Categories
                .AnyAsync(c => c.UserId == userId && c.Name.ToLower() == model.Name.ToLower().Trim());

            if (exists)
                return BadRequest(new { message = "Category already exists." });

            var category = new Category
            {
                Name = model.Name.Trim(),
                Type = model.Type,
                IsDefault = false,
                UserId = userId
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Category added successfully",
                categoryId = category.CategoryId
            });
        }

        // PUT: /api/categories/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
                return BadRequest(new { message = "Category name is required." });

            int userId = GetCurrentUserId();

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);

            if (category == null)
                return NotFound(new { message = "Category not found or not editable." });

            // Check for duplicate name (excluding current)
            var duplicate = await _context.Categories
                .AnyAsync(c => c.UserId == userId &&
                               c.CategoryId != id &&
                               c.Name.ToLower() == model.Name.ToLower().Trim());

            if (duplicate)
                return BadRequest(new { message = "Another category with this name exists." });

            category.Name = model.Name.Trim();

            await _context.SaveChangesAsync();

            return Ok(new { message = "Category updated successfully" });
        }

        // DELETE: /api/categories/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            int userId = GetCurrentUserId();

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);

            if (category == null)
                return NotFound(new { message = "Category not found or not deletable." });

            // Check if category is being used in transactions
            var inUse = await _context.Transactions
                .AnyAsync(t => t.CategoryId == id);

            if (inUse)
                return BadRequest(new
                {
                    message = "Cannot delete � category is being used by existing transactions."
                });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Category deleted successfully" });
        }
    }

    public class CategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense";
    }
}