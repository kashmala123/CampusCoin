using CampusCoin.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusCoin.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Core
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Budget> Budgets => Set<Budget>();

        // Insights / notifications / tips
        public DbSet<Insight> Insights => Set<Insight>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<SavingTip> SavingTips => Set<SavingTip>();
        public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
        public DbSet<Announcement> Announcements => Set<Announcement>();

        // Auth / activity / imports
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
        public DbSet<TipInteraction> TipInteractions => Set<TipInteraction>();
        public DbSet<CsvImportBatch> CsvImportBatches => Set<CsvImportBatch>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<Forecast> Forecasts => Set<Forecast>();
        public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

        // Gamification
        public DbSet<Badge> Badges => Set<Badge>();
        public DbSet<UserBadge> UserBadges => Set<UserBadge>();
        public DbSet<SavingsStreak> SavingsStreaks => Set<SavingsStreak>();
        public DbSet<Challenge> Challenges => Set<Challenge>();
        public DbSet<ChallengeParticipant> ChallengeParticipants => Set<ChallengeParticipant>();

        // History / corrections / exports
        public DbSet<TransactionHistory> TransactionHistories => Set<TransactionHistory>();
        public DbSet<CategoryCorrection> CategoryCorrections => Set<CategoryCorrection>();
        public DbSet<ReportExport> ReportExports => Set<ReportExport>();

        // Goals / health / AI features
        public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
        public DbSet<FinancialHealthScore> FinancialHealthScores => Set<FinancialHealthScore>();
        public DbSet<SpendingPersonality> SpendingPersonalities => Set<SpendingPersonality>();
        public DbSet<GeneratedTip> GeneratedTips => Set<GeneratedTip>();
        public DbSet<SimulationLog> SimulationLogs => Set<SimulationLog>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<FinancialCoachQuery> FinancialCoachQueries => Set<FinancialCoachQuery>();

        // Wishlist / shared expenses / benchmarks
        public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
        public DbSet<SharedExpense> SharedExpenses => Set<SharedExpense>();
        public DbSet<SharedExpenseSplit> SharedExpenseSplits => Set<SharedExpenseSplit>();
        public DbSet<CategoryBenchmark> CategoryBenchmarks => Set<CategoryBenchmark>();

        // AI Coach / Support
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

        // Fee Vouchers
        public DbSet<FeeVoucher> FeeVouchers => Set<FeeVoucher>();
        public DbSet<WalletLedger> WalletLedgers => Set<WalletLedger>();

        // Events
        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();

        
        // CMS / Homepage dynamic content
        public DbSet<SiteContent> SiteContents => Set<SiteContent>();
        public DbSet<HomepageTestimonial> HomepageTestimonials => Set<HomepageTestimonial>();
        public DbSet<HomepageFeature> HomepageFeatures => Set<HomepageFeature>();
        public DbSet<HomepageCategoryItem> HomepageCategoryItems => Set<HomepageCategoryItem>();
        public DbSet<HomepageProof> HomepageProofs => Set<HomepageProof>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Default precision for every decimal/decimal? column
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(10,2)");
            }

            // ===================== CORE =====================
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Roles");
                e.HasIndex(r => r.RoleName).IsUnique();
            });

            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.MonthlySavingsGoal).HasColumnType("decimal(10,2)");
                e.Property(u => u.MonthlyAllowanceBaseline).HasColumnType("decimal(10,2)");
                e.Property(u => u.WalletPin).HasMaxLength(256);

                e.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(e =>
            {
                e.ToTable("Categories");
                e.HasOne(c => c.User)
                    .WithMany(u => u.Categories)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Transaction>(e =>
            {
                e.ToTable("Transactions");
                e.HasOne(t => t.User)
                    .WithMany(u => u.Transactions)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(t => t.Category)
                    .WithMany(c => c.Transactions)
                    .HasForeignKey(t => t.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Budget>(e =>
            {
                e.ToTable("Budgets");
                e.HasIndex(b => new { b.UserId, b.CategoryId, b.Month }).IsUnique();
                e.HasOne(b => b.User)
                    .WithMany(u => u.Budgets)
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(b => b.Category)
                    .WithMany(c => c.Budgets)
                    .HasForeignKey(b => b.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ================= INSIGHTS / TIPS =================
            modelBuilder.Entity<Insight>(e =>
            {
                e.ToTable("Insights");
                e.Property(x => x.Month).HasColumnName("Month");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Notification>(e =>
            {
                e.ToTable("Notifications");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SavingTip>(e =>
            {
                e.ToTable("SavingTips");
                e.HasKey(x => x.TipId);
            });

            modelBuilder.Entity<Bookmark>(e =>
            {
                e.ToTable("Bookmarks");
                e.HasIndex(x => new { x.UserId, x.RefType, x.RefId }).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Announcement>(e =>
            {
                e.ToTable("Announcements");
                e.HasOne(x => x.PostedByAdmin).WithMany().HasForeignKey(x => x.PostedByAdminId).OnDelete(DeleteBehavior.Restrict);
            });

            // ================= AUTH / ACTIVITY =================
            modelBuilder.Entity<PasswordResetToken>(e =>
            {
                e.ToTable("PasswordResetTokens");
                e.HasKey(x => x.TokenId);
                e.HasIndex(x => x.Token).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RecurringTransaction>(e =>
            {
                e.ToTable("RecurringTransactions");
                e.HasKey(x => x.RecurringId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TipInteraction>(e =>
            {
                e.ToTable("TipInteractions");
                e.HasKey(x => x.InteractionId);
                e.HasIndex(x => new { x.UserId, x.TipId }).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Tip).WithMany().HasForeignKey(x => x.TipId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CsvImportBatch>(e =>
            {
                e.ToTable("CsvImportBatches");
                e.HasKey(x => x.ImportId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ActivityLog>(e =>
            {
                e.ToTable("ActivityLog");
                e.HasKey(x => x.LogId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Forecast>(e =>
            {
                e.ToTable("Forecasts");
                e.HasIndex(x => new { x.UserId, x.ForMonth }).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserPreference>(e =>
            {
                e.ToTable("UserPreferences");
                e.HasKey(x => x.UserId);
                e.HasOne(x => x.User).WithOne().HasForeignKey<UserPreference>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ================= GAMIFICATION =================
            modelBuilder.Entity<Badge>(e =>
            {
                e.ToTable("Badges");
                e.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<UserBadge>(e =>
            {
                e.ToTable("UserBadges");
                e.HasIndex(x => new { x.UserId, x.BadgeId }).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Badge).WithMany().HasForeignKey(x => x.BadgeId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SavingsStreak>(e =>
            {
                e.ToTable("SavingsStreaks");
                e.HasKey(x => x.UserId);
                e.HasOne(x => x.User).WithOne().HasForeignKey<SavingsStreak>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Challenge>(e =>
            {
                e.ToTable("Challenges");
                e.HasOne(x => x.CreatedByAdmin).WithMany().HasForeignKey(x => x.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChallengeParticipant>(e =>
            {
                e.ToTable("ChallengeParticipants");
                e.HasKey(x => x.ParticipantId);
                e.HasIndex(x => new { x.ChallengeId, x.UserId }).IsUnique();
                e.HasOne(x => x.Challenge).WithMany().HasForeignKey(x => x.ChallengeId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ================= HISTORY / CORRECTIONS / EXPORTS =================
            modelBuilder.Entity<TransactionHistory>(e =>
            {
                e.ToTable("TransactionHistory");
                e.HasKey(x => x.HistoryId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CategoryCorrection>(e =>
            {
                e.ToTable("CategoryCorrections");
                e.HasKey(x => x.CorrectionId);
                e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.SuggestedCategory).WithMany().HasForeignKey(x => x.SuggestedCategoryId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ActualCategory).WithMany().HasForeignKey(x => x.ActualCategoryId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ReportExport>(e =>
            {
                e.ToTable("ReportExports");
                e.HasKey(x => x.ExportId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ================= GOALS / HEALTH / AI =================
            modelBuilder.Entity<SavingsGoal>(e =>
            {
                e.ToTable("SavingsGoals");
                e.HasKey(x => x.GoalId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FinancialHealthScore>(e =>
            {
                e.ToTable("FinancialHealthScores");
                e.HasKey(x => x.ScoreId);
                e.HasIndex(x => new { x.UserId, x.ForMonth }).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SpendingPersonality>(e =>
            {
                e.ToTable("SpendingPersonality");
                e.HasKey(x => x.UserId);
                e.HasOne(x => x.User).WithOne().HasForeignKey<SpendingPersonality>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GeneratedTip>(e =>
            {
                e.ToTable("GeneratedTips");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Tip).WithMany().HasForeignKey(x => x.TipId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SimulationLog>(e =>
            {
                e.ToTable("SimulationLogs");
                e.HasKey(x => x.SimulationId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserSession>(e =>
            {
                e.ToTable("UserSessions");
                e.HasKey(x => x.SessionId);
                e.HasIndex(x => x.SessionToken).IsUnique();
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FinancialCoachQuery>(e =>
            {
                e.ToTable("FinancialCoachQueries");
                e.HasKey(x => x.QueryId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ================= WISHLIST / SHARED / BENCHMARK =================
            modelBuilder.Entity<WishlistItem>(e =>
            {
                e.ToTable("WishlistItems");
                e.HasKey(x => x.WishlistId);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SharedExpense>(e =>
            {
                e.ToTable("SharedExpenses");
                e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.PayerUser).WithMany().HasForeignKey(x => x.PayerUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SharedExpenseSplit>(e =>
            {
                e.ToTable("SharedExpenseSplits");
                e.HasKey(x => x.SplitId);
                e.HasOne(x => x.SharedExpense).WithMany().HasForeignKey(x => x.SharedExpenseId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CategoryBenchmark>(e =>
            {
                e.ToTable("CategoryBenchmarks");
                e.HasKey(x => x.BenchmarkId);
                e.HasIndex(x => new { x.CategoryId, x.ForMonth }).IsUnique();
                e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            });

            // ================= AI COACH / SUPPORT =================
            modelBuilder.Entity<ChatMessage>(e =>
            {
                e.ToTable("ChatMessages");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.CreatedAt });
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupportTicket>(e =>
            {
                e.ToTable("SupportTickets");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.TicketCode).IsUnique();
                e.HasIndex(x => new { x.UserId, x.Status });
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ================= FEE VOUCHERS =================
            modelBuilder.Entity<FeeVoucher>(e =>
            {
                e.ToTable("FeeVouchers");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.VoucherCode).IsUnique();
                e.HasIndex(x => new { x.UserId, x.Status });
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.Fine).HasDefaultValue(0m);
                e.Property(x => x.Status).HasDefaultValue("Pending");
            });

            // ================= EVENTS =================
            modelBuilder.Entity<Event>(e =>
            {
                e.ToTable("Events");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.EventCode).IsUnique();
                e.HasIndex(x => new { x.Status, x.Category });
            });

            modelBuilder.Entity<EventRegistration>(e =>
            {
                e.ToTable("EventRegistrations");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.PassCode).IsUnique();
                e.HasIndex(x => new { x.UserId, x.EventId }).IsUnique();
                e.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<SiteContent>(e =>
            {
                e.ToTable("SiteContents");
                e.HasIndex(x => x.ContentKey).IsUnique();
            });
            modelBuilder.Entity<HomepageTestimonial>(e => e.ToTable("HomepageTestimonials"));
            modelBuilder.Entity<HomepageFeature>(e => e.ToTable("HomepageFeatures"));
            modelBuilder.Entity<HomepageCategoryItem>(e =>
            {
                e.ToTable("HomepageCategoryItems");
                e.Property(x => x.Percent).HasColumnName("Percent");
                e.Property(x => x.Group).HasColumnName("Group");
            });
            modelBuilder.Entity<HomepageProof>(e => e.ToTable("HomepageProofs"));
        }
    }
}
