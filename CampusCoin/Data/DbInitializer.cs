using CampusCoin.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CampusCoin.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, IConfiguration? configuration = null)
        {
            // Production-safe: apply EF Core migrations (creates DB if missing, updates schema on deploy).
            // Prefer Migrate over EnsureCreated so future model changes deploy cleanly.
            try
            {
                await context.Database.MigrateAsync();
                Console.WriteLine("Database migrations applied successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database.MigrateAsync error: {ex.Message}");
                // Fallback for DBs that were created earlier with EnsureCreated (no __EFMigrationsHistory).
                // Ensures local/mentor machines still start; production should use a clean DB + migrations.
                try
                {
                    await context.Database.EnsureCreatedAsync();
                    Console.WriteLine("Fallback EnsureCreated completed.");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Database EnsureCreated fallback error: {ex2.Message}");
                }
            }

            // Make sure the tables/columns the login flow needs exist, even if the
            // database was created from an older version of the SQL script.
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Users', N'IsActive') IS NULL
    ALTER TABLE dbo.Users ADD IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Users', N'MonthlyAllowanceBaseline') IS NULL
    ALTER TABLE dbo.Users ADD MonthlyAllowanceBaseline DECIMAL(10,2) NOT NULL CONSTRAINT DF_Users_MonthlyAllowanceBaseline DEFAULT 0;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Users', N'WalletPin') IS NULL
    ALTER TABLE dbo.Users ADD WalletPin NVARCHAR(20) NULL;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
    CREATE TABLE dbo.UserSessions (
        SessionId    INT IDENTITY(1,1) PRIMARY KEY,
        UserId       INT           NOT NULL,
        SessionToken VARCHAR(255)  NOT NULL UNIQUE,
        IpAddress    VARCHAR(45)   NULL,
        CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE(),
        ExpiresAt    DATETIME      NOT NULL,
        IsRevoked    BIT           NOT NULL DEFAULT 0,
        CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

-- Bookmarks snapshot columns required by dashboard pin / Bookmarks page
IF OBJECT_ID(N'dbo.Bookmarks', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Bookmarks', N'Title') IS NULL
    ALTER TABLE dbo.Bookmarks ADD Title NVARCHAR(300) NULL;
IF OBJECT_ID(N'dbo.Bookmarks', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Bookmarks', N'Content') IS NULL
    ALTER TABLE dbo.Bookmarks ADD Content NVARCHAR(MAX) NULL;
IF OBJECT_ID(N'dbo.Bookmarks', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Bookmarks', N'Category') IS NULL
    ALTER TABLE dbo.Bookmarks ADD Category NVARCHAR(100) NULL;

-- Ensure Bookmarks table exists even on older DBs
IF OBJECT_ID(N'dbo.Bookmarks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bookmarks (
        BookmarkId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        RefType NVARCHAR(450) NOT NULL,
        RefId INT NOT NULL,
        Title NVARCHAR(300) NULL,
        Content NVARCHAR(MAX) NULL,
        Category NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_Bookmarks_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_Bookmarks_UserId_RefType_RefId ON dbo.Bookmarks(UserId, RefType, RefId);
END

-- Wallet ledger table (real wallet movement history)
IF OBJECT_ID(N'dbo.WalletLedgers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalletLedgers (
        LedgerId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        EntryType NVARCHAR(30) NOT NULL,
        Direction NVARCHAR(10) NOT NULL,
        Amount DECIMAL(12,2) NOT NULL,
        BalanceAfter DECIMAL(12,2) NOT NULL,
        Method NVARCHAR(40) NULL,
        CounterpartyAccount NVARCHAR(40) NULL,
        CounterpartyName NVARCHAR(150) NULL,
        Description NVARCHAR(250) NULL,
        ReferenceCode NVARCHAR(80) NULL,
        RelatedUserId INT NULL,
        RelatedTransactionId INT NULL,
        RelatedFeeVoucherId INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WalletLedgers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );
    CREATE INDEX IX_WalletLedgers_UserId_CreatedAt ON dbo.WalletLedgers(UserId, CreatedAt DESC);
END

");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema self-check warning: {ex.Message}");
            }

            // 1. Roles
            if (!await context.Roles.AnyAsync())
            {
                context.Roles.AddRange(
                    new Role { RoleName = "Admin" },
                    new Role { RoleName = "Student" },
                    new Role { RoleName = "DemoAdmin" }
                );
                await context.SaveChangesAsync();
            }

            // Ensure DemoAdmin exists on databases seeded before this change
            if (!await context.Roles.AnyAsync(r => r.RoleName == "DemoAdmin"))
            {
                context.Roles.Add(new Role { RoleName = "DemoAdmin" });
                await context.SaveChangesAsync();
            }

            var adminRole = await context.Roles.FirstAsync(r => r.RoleName == "Admin");
            var studentRole = await context.Roles.FirstAsync(r => r.RoleName == "Student");
            var demoAdminRole = await context.Roles.FirstAsync(r => r.RoleName == "DemoAdmin");

            // 2. Default Categories
            var defaultCategories = new List<(string Name, string Type)>
            {
                ("Allowance", "Income"),
                ("Part-time Job", "Income"),
                ("Scholarship", "Income"),
                ("Gift", "Income"),
                ("Other Income", "Income"),
                ("Food", "Expense"),
                ("Transport", "Expense"),
                ("Hostel/Rent", "Expense"),
                ("Academics", "Expense"),
                ("Subscriptions", "Expense"),
                ("Entertainment", "Expense"),
                ("Miscellaneous", "Expense")
            };

            foreach (var (name, type) in defaultCategories)
            {
                if (!await context.Categories.AnyAsync(c => c.Name == name && c.IsDefault))
                {
                    context.Categories.Add(new Category
                    {
                        Name = name,
                        Type = type,
                        IsDefault = true,
                        UserId = null
                    });
                }
            }
            await context.SaveChangesAsync();

            // 3. Default Admin User
            User? adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@campuscoin.com");
            if (adminUser == null)
            {
                var adminPassword =
                    configuration?["Seed:AdminPassword"]
                    ?? Environment.GetEnvironmentVariable("CAMPUSCOIN_ADMIN_PASSWORD");

                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    Console.WriteLine(
                        "Admin user NOT seeded. Set Seed:AdminPassword (User Secrets) " +
                        "or environment variable CAMPUSCOIN_ADMIN_PASSWORD, then restart the app.");
                }
                else
                {
                    adminUser = new User
                    {
                        FullName = "System Administrator",
                        Email = "admin@campuscoin.com",
                        AcademicYear = "Faculty/Admin",
                        MonthlyAllowanceBaseline = 0m,
                        MonthlySavingsGoal = 0m,
                        RoleId = adminRole.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, adminPassword);
                    context.Users.Add(adminUser);
                    await context.SaveChangesAsync();
                    Console.WriteLine("Admin user seeded: admin@campuscoin.com");
                }
            }

            // 3b. Demo Admin (read-only portfolio account) — password from config/env only
            const string demoAdminEmail = "demo.admin@campuscoin-demo.com";
            User? demoAdminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == demoAdminEmail);
            if (demoAdminUser == null)
            {
                var demoPassword =
                    configuration?["Seed:DemoAdminPassword"]
                    ?? Environment.GetEnvironmentVariable("CAMPUSCOIN_DEMO_ADMIN_PASSWORD");

                if (string.IsNullOrWhiteSpace(demoPassword))
                {
                    Console.WriteLine(
                        "Demo Admin user NOT seeded. Set Seed:DemoAdminPassword (User Secrets) " +
                        "or environment variable CAMPUSCOIN_DEMO_ADMIN_PASSWORD, then restart the app.");
                }
                else
                {
                    demoAdminUser = new User
                    {
                        FullName = "Demo Administrator (Read-Only)",
                        Email = demoAdminEmail,
                        AcademicYear = "Faculty/Admin",
                        MonthlyAllowanceBaseline = 0m,
                        MonthlySavingsGoal = 0m,
                        RoleId = demoAdminRole.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    demoAdminUser.PasswordHash = passwordHasher.HashPassword(demoAdminUser, demoPassword);
                    context.Users.Add(demoAdminUser);
                    await context.SaveChangesAsync();
                    Console.WriteLine("Demo Admin user seeded: " + demoAdminEmail);
                }
            }

            // 4. Default Demo Student User (Alex Rivera & Ayesha Khan)
            User? demoStudent = await context.Users.FirstOrDefaultAsync(u => u.Email == "alex@campuscoin.com");
            if (demoStudent == null)
            {
                var studentPassword =
                    configuration?["Seed:StudentPassword"]
                    ?? Environment.GetEnvironmentVariable("CAMPUSCOIN_STUDENT_PASSWORD");

                if (string.IsNullOrWhiteSpace(studentPassword))
                {
                    Console.WriteLine(
                        "Demo student (alex@campuscoin.com) NOT seeded. Set Seed:StudentPassword " +
                        "or environment variable CAMPUSCOIN_STUDENT_PASSWORD, then restart the app.");
                }
                else
                {
                    demoStudent = new User
                    {
                        FullName = "Alex Rivera",
                        Email = "alex@campuscoin.com",
                        AcademicYear = "2nd Year",
                        MonthlyAllowanceBaseline = 1000.00m,
                        MonthlySavingsGoal = 300.00m,
                        RoleId = studentRole.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    demoStudent.PasswordHash = passwordHasher.HashPassword(demoStudent, studentPassword);
                    context.Users.Add(demoStudent);
                    await context.SaveChangesAsync();
                    Console.WriteLine("Demo student seeded: alex@campuscoin.com");
                }
            }

            // Also ensure ayesha@campus.edu exists for compatibility with SQL seed script
            var ayesha = await context.Users.FirstOrDefaultAsync(u => u.Email == "ayesha@campus.edu");
            if (ayesha == null)
            {
                var studentPasswordAyesha =
                    configuration?["Seed:StudentPassword"]
                    ?? Environment.GetEnvironmentVariable("CAMPUSCOIN_STUDENT_PASSWORD");

                if (string.IsNullOrWhiteSpace(studentPasswordAyesha))
                {
                    Console.WriteLine(
                        "Demo student (ayesha@campus.edu) NOT seeded. Set Seed:StudentPassword " +
                        "or environment variable CAMPUSCOIN_STUDENT_PASSWORD, then restart the app.");
                }
                else
                {
                    ayesha = new User
                    {
                        FullName = "Ayesha Khan",
                        Email = "ayesha@campus.edu",
                        AcademicYear = "3rd Year",
                        MonthlyAllowanceBaseline = 1250.00m,
                        MonthlySavingsGoal = 500.00m,
                        RoleId = studentRole.RoleId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    ayesha.PasswordHash = passwordHasher.HashPassword(ayesha, studentPasswordAyesha);
                    context.Users.Add(ayesha);
                    await context.SaveChangesAsync();
                    Console.WriteLine("Demo student seeded: ayesha@campus.edu");
                }
            }

            // 5. Default Saving Tips templates
            if (!await context.SavingTips.AnyAsync())
            {
                context.SavingTips.AddRange(
                    new SavingTip
                    {
                        TipText = "Dining out took significant spending this week. Cooking twice more saves ~$45/week.",
                        TriggerCategory = "Food",
                        TriggerType = "PercentAbove",
                        TriggerValue = 30.00m,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SavingTip
                    {
                        TipText = "Rent or buy digital textbooks early to save up to 35% on course materials.",
                        TriggerCategory = "Academics",
                        TriggerType = "PercentAbove",
                        TriggerValue = 15.00m,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SavingTip
                    {
                        TipText = "Review active digital subscriptions to eliminate unused streaming or gaming services.",
                        TriggerCategory = "Subscriptions",
                        TriggerType = "OverBudget",
                        TriggerValue = null,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SavingTip
                    {
                        TipText = "Your savings rate is below 10% — consider setting a weekly spending cap to meet your goals.",
                        TriggerCategory = null,
                        TriggerType = "SavingsBelow",
                        TriggerValue = 10.00m,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new SavingTip
                    {
                        TipText = "Consider shared campus rides or public transit passes to cut weekly transport expenses.",
                        TriggerCategory = "Transport",
                        TriggerType = "PercentAbove",
                        TriggerValue = 20.00m,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                );
                await context.SaveChangesAsync();
            }

            // Re-fetch demo student in case it was just created or already existed
            demoStudent = await context.Users.FirstOrDefaultAsync(u => u.Email == "alex@campuscoin.com");

            // 6. Seed sample data for Alex Rivera if no transactions exist
            if (demoStudent != null && !await context.Transactions.AnyAsync(t => t.UserId == demoStudent.UserId))
            {
                var foodCat = await context.Categories.FirstAsync(c => c.Name == "Food" && c.IsDefault);
                var transCat = await context.Categories.FirstAsync(c => c.Name == "Transport" && c.IsDefault);
                var acadCat = await context.Categories.FirstAsync(c => c.Name == "Academics" && c.IsDefault);
                var entCat = await context.Categories.FirstAsync(c => c.Name == "Entertainment" && c.IsDefault);
                var allowCat = await context.Categories.FirstAsync(c => c.Name == "Allowance" && c.IsDefault);
                var jobCat = await context.Categories.FirstAsync(c => c.Name == "Part-time Job" && c.IsDefault);

                var now = DateTime.UtcNow;
                var currentMonthStart = new DateTime(now.Year, now.Month, 1);

                context.Transactions.AddRange(
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = allowCat.CategoryId,
                        Amount = 1000.00m,
                        Type = "Income",
                        Description = "Monthly TA Allowance Baseline",
                        Date = currentMonthStart.AddDays(1),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = jobCat.CategoryId,
                        Amount = 250.00m,
                        Type = "Income",
                        Description = "Tutoring Payment",
                        Date = currentMonthStart.AddDays(15),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = foodCat.CategoryId,
                        Amount = 14.50m,
                        Type = "Expense",
                        Description = "KFC Campus Combo",
                        Date = currentMonthStart.AddDays(20),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = transCat.CategoryId,
                        Amount = 8.20m,
                        Type = "Expense",
                        Description = "Careem Ride to Library",
                        Date = currentMonthStart.AddDays(19),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = acadCat.CategoryId,
                        Amount = 45.00m,
                        Type = "Expense",
                        Description = "CS Textbook PDF",
                        Date = currentMonthStart.AddDays(18),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = entCat.CategoryId,
                        Amount = 12.50m,
                        Type = "Expense",
                        Description = "Cinema Movie Ticket",
                        Date = currentMonthStart.AddDays(14),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = foodCat.CategoryId,
                        Amount = 4.80m,
                        Type = "Expense",
                        Description = "Cafeteria Coffee",
                        Date = currentMonthStart.AddDays(12),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = foodCat.CategoryId,
                        Amount = 180.00m,
                        Type = "Expense",
                        Description = "Campus Dining & Groceries",
                        Date = currentMonthStart.AddDays(10),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = transCat.CategoryId,
                        Amount = 75.00m,
                        Type = "Expense",
                        Description = "Monthly Transit Pass",
                        Date = currentMonthStart.AddDays(5),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = acadCat.CategoryId,
                        Amount = 140.00m,
                        Type = "Expense",
                        Description = "Lab Supplies & Notebooks",
                        Date = currentMonthStart.AddDays(3),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = entCat.CategoryId,
                        Amount = 60.20m,
                        Type = "Expense",
                        Description = "Concert & Weekend Outing",
                        Date = currentMonthStart.AddDays(8),
                        CreatedAt = DateTime.UtcNow
                    }
                );

                // Sample Budgets
                context.Budgets.AddRange(
                    new Budget
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = foodCat.CategoryId,
                        Month = currentMonthStart,
                        LimitAmount = 300.00m
                    },
                    new Budget
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = transCat.CategoryId,
                        Month = currentMonthStart,
                        LimitAmount = 120.00m
                    },
                    new Budget
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = acadCat.CategoryId,
                        Month = currentMonthStart,
                        LimitAmount = 250.00m
                    },
                    new Budget
                    {
                        UserId = demoStudent.UserId,
                        CategoryId = entCat.CategoryId,
                        Month = currentMonthStart,
                        LimitAmount = 100.00m
                    }
                );

                // Sample Savings Goal
                context.SavingsGoals.Add(new SavingsGoal
                {
                    UserId = demoStudent.UserId,
                    GoalName = "Emergency Fund Buffer",
                    TargetAmount = 1600.00m,
                    CurrentAmount = 1480.00m,
                    TargetDate = now.AddMonths(3),
                    IsAchieved = false,
                    CreatedAt = DateTime.UtcNow
                });

                // Sample Notification
                context.Notifications.Add(new Notification
                {
                    UserId = demoStudent.UserId,
                    Message = "Your monthly scholarship ($450) was deposited!",
                    Type = "Info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }

            // ── CMS homepage content (tables + seed) ──
            try
            {
                async Task ExecCms(string sql)
                {
                    try { await context.Database.ExecuteSqlRawAsync(sql); }
                    catch (Exception ex) { Console.WriteLine("CMS SQL warn: " + ex.Message); }
                }
                await ExecCms(@"
IF OBJECT_ID(N'dbo.SiteContents', N'U') IS NULL
CREATE TABLE dbo.SiteContents (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContentKey NVARCHAR(80) NOT NULL,
    Value NVARCHAR(MAX) NOT NULL,
    Label NVARCHAR(200) NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);");
                await ExecCms(@"
IF OBJECT_ID(N'dbo.SiteContents', N'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SiteContents_ContentKey' AND object_id = OBJECT_ID(N'dbo.SiteContents'))
CREATE UNIQUE INDEX IX_SiteContents_ContentKey ON dbo.SiteContents(ContentKey);");
                await ExecCms(@"
IF OBJECT_ID(N'dbo.HomepageTestimonials', N'U') IS NULL
CREATE TABLE dbo.HomepageTestimonials (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Quote NVARCHAR(600) NOT NULL,
    AuthorName NVARCHAR(80) NOT NULL,
    AuthorMeta NVARCHAR(120) NOT NULL DEFAULT N'',
    AvatarInitials NVARCHAR(8) NOT NULL DEFAULT N'ST',
    MetricLabel NVARCHAR(80) NULL,
    MetricValue NVARCHAR(40) NULL,
    MetricBadge NVARCHAR(40) NULL,
    Tags NVARCHAR(120) NULL,
    Tone NVARCHAR(20) NOT NULL DEFAULT N'mint',
    IsFeatured BIT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
                await ExecCms(@"
IF OBJECT_ID(N'dbo.HomepageFeatures', N'U') IS NULL
CREATE TABLE dbo.HomepageFeatures (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(120) NOT NULL,
    Description NVARCHAR(400) NOT NULL,
    Icon NVARCHAR(60) NOT NULL DEFAULT N'ri-sparkling-2-line',
    Status NVARCHAR(40) NOT NULL DEFAULT N'Live',
    Tone NVARCHAR(20) NOT NULL DEFAULT N'violet',
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
                await ExecCms(@"
IF OBJECT_ID(N'dbo.HomepageCategoryItems', N'U') IS NULL
CREATE TABLE dbo.HomepageCategoryItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    [Group] NVARCHAR(20) NOT NULL,
    Name NVARCHAR(80) NOT NULL,
    Icon NVARCHAR(60) NOT NULL DEFAULT N'ri-price-tag-3-line',
    Amount DECIMAL(12,2) NOT NULL DEFAULT 0,
    [Percent] INT NOT NULL DEFAULT 0,
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
                await ExecCms(@"
IF OBJECT_ID(N'dbo.HomepageProofs', N'U') IS NULL
CREATE TABLE dbo.HomepageProofs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Label NVARCHAR(80) NOT NULL,
    Value NVARCHAR(40) NOT NULL,
    Badge NVARCHAR(40) NOT NULL DEFAULT N'Verified',
    Tone NVARCHAR(20) NOT NULL DEFAULT N'mint',
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CMS table ensure warning: {ex.Message}");
            }

            if (!await context.SiteContents.AnyAsync())
            {
                var contents = new (string Key, string Value, string Label)[]
                {
                    ("hero_label", "Student money, made clear", "Hero label"),
                    ("hero_title", "Know where your money goes.", "Hero title"),
                    ("hero_subtitle", "Track spending, understand your habits, and build better financial decisions — all in one beautiful workspace.", "Hero subtitle"),
                    ("hero_cta", "Get Started", "Hero primary button"),
                    ("hero_link", "Explore all features", "Hero secondary link"),
                    ("features_label", "Built for student life", "Features label"),
                    ("features_title", "Everything you need to stay on top of campus money.", "Features title"),
                    ("features_subtitle", "From daily spends to semester goals — soft tools that feel natural.", "Features subtitle"),
                    ("categories_label", "Categories that fit real life", "Categories label"),
                    ("categories_title", "Built for how students actually spend.", "Categories title"),
                    ("categories_subtitle", "From canteen food to hostel rent — every category is designed around student life. Add your own any time.", "Categories subtitle"),
                    ("categories_cta_title", "Manage your own categories", "Categories CTA title"),
                    ("categories_cta_text", "Add, edit, or delete any category — your categories, your rules.", "Categories CTA text"),
                    ("categories_cta_button", "Try it", "Categories CTA button"),
                    ("testimonials_label", "Loved by students", "Testimonials label"),
                    ("testimonials_title", "Real students. Real savings.", "Testimonials title"),
                    ("testimonials_subtitle", "Over 12,000 students use CashCampus to track allowances, spot habits, and build better money patterns.", "Testimonials subtitle"),
                    ("newsletter_title", "Money tips in your inbox", "Newsletter title"),
                    ("newsletter_subtitle", "Short, useful notes for campus life — no spam.", "Newsletter subtitle"),
                    ("newsletter_button", "Subscribe", "Newsletter button"),
                    ("contact_label", "Get in touch", "Contact label"),
                    ("contact_title", "Questions? We're here.", "Contact title"),
                    ("contact_subtitle", "Send a message and we'll get back within one working day.", "Contact subtitle"),

                    ("show_features", "1", "Show features section"),
                    ("show_categories", "1", "Show categories section"),
                    ("show_manage_bar", "1", "Show manage categories bar"),
                    ("show_testimonials", "1", "Show testimonials section"),
                    ("show_proofs", "1", "Show proof chips"),
                    ("show_newsletter", "1", "Show newsletter section"),
                    ("show_contact", "1", "Show contact section"),
                    ("hero_badge", "Student money, made clear", "Hero badge"),
                    ("footer_tagline", "Money tools built for campus life.", "Footer tagline"),
                    ("ai_coach_placeholder", "Ask about your spending...", "AI coach placeholder"),
                    ("cta_secondary_label", "Explore all features", "Secondary CTA label"),
                    ("income_total", "32500", "Demo income total"),
                    ("expense_total", "24850", "Demo expense total"),
                };
                foreach (var (k, v, l) in contents)
                {
                    context.SiteContents.Add(new SiteContent { ContentKey = k, Value = v, Label = l, UpdatedAt = DateTime.UtcNow });
                }
                await context.SaveChangesAsync();
            }

            if (!await context.HomepageTestimonials.AnyAsync())
            {
                context.HomepageTestimonials.AddRange(
                    new HomepageTestimonial
                    {
                        Quote = "Finally an app that understands hostel life. I stopped losing track of my allowance by the second week.",
                        AuthorName = "Aarav K.",
                        AuthorMeta = "2nd year · Engineering",
                        AvatarInitials = "AK",
                        MetricLabel = "Saved in 2 months",
                        MetricValue = "Rs. 3,200",
                        MetricBadge = "Real result",
                        Tags = "Hostel life,Allowance",
                        Tone = "mint",
                        SortOrder = 1,
                        IsActive = true
                    },
                    new HomepageTestimonial
                    {
                        Quote = "The AI summary is scary accurate. It caught my food delivery habit before I did.",
                        AuthorName = "Maya R.",
                        AuthorMeta = "3rd year · Design",
                        AvatarInitials = "MR",
                        MetricLabel = "Cut food delivery by",
                        MetricValue = "40%",
                        MetricBadge = "Verified",
                        Tags = "AI tips,Food",
                        Tone = "rose",
                        IsFeatured = true,
                        SortOrder = 2,
                        IsActive = true
                    },
                    new HomepageTestimonial
                    {
                        Quote = "No bank linking. That alone made me trust it. Setup took under two minutes.",
                        AuthorName = "Samir T.",
                        AuthorMeta = "1st year · Business",
                        AvatarInitials = "ST",
                        MetricLabel = "Privacy-first setup",
                        MetricValue = "0 bank links",
                        MetricBadge = "Verified",
                        Tags = "Privacy,Setup",
                        Tone = "violet",
                        SortOrder = 3,
                        IsActive = true
                    }
                );
                await context.SaveChangesAsync();
            }

            if (!await context.HomepageFeatures.AnyAsync())
            {
                context.HomepageFeatures.AddRange(
                    new HomepageFeature { Title = "See your real balance", Description = "Live totals for income, expenses, and what's left — updated as you log.", Icon = "ri-wallet-3-line", Status = "Live", Tone = "violet", SortOrder = 1 },
                    new HomepageFeature { Title = "Friendly AI insights", Description = "Plain-language summaries that tell you what changed — and one small thing you can try next.", Icon = "ri-sparkling-2-line", Status = "Online", Tone = "mint", SortOrder = 2 },
                    new HomepageFeature { Title = "Budgets without the guilt", Description = "Set category limits and watch progress through soft, visual indicators.", Icon = "ri-pie-chart-2-line", Status = "On track", Tone = "coral", SortOrder = 3 },
                    new HomepageFeature { Title = "Reports you can share", Description = "Weekly and monthly views that make sense for allowance life.", Icon = "ri-bar-chart-box-line", Status = "Ready", Tone = "amber", SortOrder = 4 }
                );
                await context.SaveChangesAsync();
            }

            if (!await context.HomepageCategoryItems.AnyAsync())
            {
                context.HomepageCategoryItems.AddRange(
                    new HomepageCategoryItem { Group = "Income", Name = "Allowance", Icon = "ri-hand-coin-line", Amount = 18000, Percent = 55, SortOrder = 1 },
                    new HomepageCategoryItem { Group = "Income", Name = "Part-time job", Icon = "ri-briefcase-4-line", Amount = 9500, Percent = 29, SortOrder = 2 },
                    new HomepageCategoryItem { Group = "Income", Name = "Scholarship", Icon = "ri-graduation-cap-line", Amount = 4000, Percent = 12, SortOrder = 3 },
                    new HomepageCategoryItem { Group = "Income", Name = "Gift", Icon = "ri-gift-2-line", Amount = 1000, Percent = 3, SortOrder = 4 },
                    new HomepageCategoryItem { Group = "Income", Name = "Freelance", Icon = "ri-code-box-line", Amount = 2000, Percent = 6, SortOrder = 5 },
                    new HomepageCategoryItem { Group = "Expense", Name = "Food", Icon = "ri-restaurant-line", Amount = 8420, Percent = 34, SortOrder = 1 },
                    new HomepageCategoryItem { Group = "Expense", Name = "Hostel / Rent", Icon = "ri-home-4-line", Amount = 6500, Percent = 26, SortOrder = 2 },
                    new HomepageCategoryItem { Group = "Expense", Name = "Transport", Icon = "ri-bus-line", Amount = 3180, Percent = 13, SortOrder = 3 },
                    new HomepageCategoryItem { Group = "Expense", Name = "Academics", Icon = "ri-book-2-line", Amount = 2240, Percent = 9, SortOrder = 4 },
                    new HomepageCategoryItem { Group = "Expense", Name = "Subscriptions", Icon = "ri-netflix-line", Amount = 1890, Percent = 8, SortOrder = 5 }
                );
                await context.SaveChangesAsync();
            }

            if (!await context.HomepageProofs.AnyAsync())
            {
                context.HomepageProofs.AddRange(
                    new HomepageProof { Label = "Saved in 2 months", Value = "Rs. 3,200", Badge = "Real result", Tone = "violet", SortOrder = 1 },
                    new HomepageProof { Label = "Cut food delivery by", Value = "40%", Badge = "Verified", Tone = "rose", SortOrder = 2 },
                    new HomepageProof { Label = "Privacy-first setup", Value = "0 bank links", Badge = "Verified", Tone = "mint", SortOrder = 3 }
                );
                await context.SaveChangesAsync();
            }

        }
    }
}
