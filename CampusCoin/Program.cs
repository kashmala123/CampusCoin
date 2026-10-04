using CampusCoin.Data;
using CampusCoin.Models;
using CampusCoin.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers and Views with camelCase JSON serialization
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// 2. Register EF Core with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Register Application Services & Security
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<FinancialHealthService>();
builder.Services.AddScoped<SmartTipsEngine>();

// Gemini AI Service
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<GeminiService>();

// Email (SMTP) — contact form, newsletter, password reset
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// 4. Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "CampusCoin.Session";

        // CRITICAL: /api/* must return JSON 401/403 (not HTML login redirect)
        // otherwise admin/student fetch() fails and dynamic data stays empty.
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"success\":false,\"message\":\"Unauthorized. Please login again.\"}");
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"success\":false,\"message\":\"Access denied.\"}");
            }
            var role = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var target = (role == "Admin" || role == "DemoAdmin") ? "/Admin" : "/";
            context.Response.Redirect(target);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AdminRead", policy => policy.RequireRole("Admin", "DemoAdmin"));
    options.AddPolicy("StudentOnly", policy => policy.RequireRole("Student"));
});

var app = builder.Build();

// 5. Apply EF migrations (deploy-safe) + seed initial data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var config = services.GetRequiredService<IConfiguration>();
        await DbInitializer.InitializeAsync(db, hasher, config);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing seed data.");
    }
}

// 6. Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/Home/NotFound", "?code={0}");
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Required for [ApiController] attribute-routed endpoints (Bookmarks, Tips, Insights, etc.)
app.MapControllers();

app.Run();