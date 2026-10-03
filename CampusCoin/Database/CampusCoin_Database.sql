/* ============================================================
   CAMPUSCOIN DATABASE  —  Smart Spending, Student Style
   SQL Server Script  |  Version 1.0
   ============================================================
   Run this whole file in SQL Server Management Studio (SSMS)
   or through the "New Query" window connected to your server.
   ============================================================ */

IF DB_ID('CampusCoinDb') IS NOT NULL
BEGIN
    ALTER DATABASE CampusCoinDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE CampusCoinDb;
END
GO

CREATE DATABASE CampusCoinDb;
GO

USE CampusCoinDb;
GO

/* ============================================================
   1. ROLES  (Admin / Student — used for role-based login)
   ============================================================ */
CREATE TABLE Roles (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    VARCHAR(20) NOT NULL UNIQUE
);
GO

/* ============================================================
   2. USERS
   ============================================================ */
CREATE TABLE Users (
    UserId               INT IDENTITY(1,1) PRIMARY KEY,
    FullName             VARCHAR(100)     NOT NULL,
    Email                VARCHAR(150)     NOT NULL UNIQUE,          -- UNIQUE KEY
    PasswordHash         VARCHAR(255)     NOT NULL,
    AcademicYear         VARCHAR(20)      NULL,
    MonthlySavingsGoal   DECIMAL(10,2)    NOT NULL DEFAULT 0,
    RoleId               INT              NOT NULL,                  -- FOREIGN KEY
    CreatedAt            DATETIME         NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId)
        REFERENCES Roles(RoleId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   3. CATEGORIES
   ------------------------------------------------------------
   UserId is NULLable -> default/system categories belong to
   nobody (UserId = NULL). Student's own categories have UserId.
   ============================================================ */
CREATE TABLE Categories (
    CategoryId    INT IDENTITY(1,1) PRIMARY KEY,
    Name          VARCHAR(50)  NOT NULL,
    Type          VARCHAR(10)  NOT NULL CHECK (Type IN ('Income','Expense')),
    IsDefault     BIT          NOT NULL DEFAULT 0,
    UserId        INT          NULL,                                 -- FOREIGN KEY (nullable)

    CONSTRAINT FK_Categories_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   4. TRANSACTIONS  (core table — every income/expense entry)
   ============================================================ */
CREATE TABLE Transactions (
    TransactionId         INT IDENTITY(1,1) PRIMARY KEY,
    UserId                INT           NOT NULL,                     -- FOREIGN KEY
    CategoryId            INT           NOT NULL,                     -- FOREIGN KEY
    Amount                DECIMAL(10,2) NOT NULL CHECK (Amount > 0),
    Type                  VARCHAR(10)   NOT NULL CHECK (Type IN ('Income','Expense')),
    Description           VARCHAR(250)  NULL,
    AiSuggestedCategoryId INT           NULL,
    [Date]                DATE          NOT NULL,
    CreatedAt             DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Transactions_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_Transactions_Categories FOREIGN KEY (CategoryId)
        REFERENCES Categories(CategoryId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   5. BUDGETS
   ------------------------------------------------------------
   UNIQUE constraint: ek user ki ek category ka ek month mein
   sirf EK hi budget row ho sakti hai.
   ============================================================ */
CREATE TABLE Budgets (
    BudgetId       INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT           NOT NULL,                            -- FOREIGN KEY
    CategoryId     INT           NOT NULL,                            -- FOREIGN KEY
    [Month]        DATE          NOT NULL,                            -- store as first day of month
    LimitAmount    DECIMAL(10,2) NOT NULL CHECK (LimitAmount > 0),

    CONSTRAINT FK_Budgets_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_Budgets_Categories FOREIGN KEY (CategoryId)
        REFERENCES Categories(CategoryId)
        ON DELETE NO ACTION,

    CONSTRAINT UQ_Budgets_User_Category_Month
        UNIQUE (UserId, CategoryId, [Month])                          -- UNIQUE KEY (composite)
);
GO

/* ============================================================
   6. INSIGHTS  (AI / smart-tips generated summaries)
   ============================================================ */
CREATE TABLE Insights (
    InsightId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT       NOT NULL,                                -- FOREIGN KEY
    [Month]        DATE      NOT NULL,
    SummaryText    VARCHAR(500) NULL,
    TipText        VARCHAR(500) NULL,
    GeneratedAt    DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Insights_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   7. NOTIFICATIONS  (budget-alert / in-app notifications)
   ============================================================ */
CREATE TABLE Notifications (
    NotificationId  INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT           NOT NULL,                           -- FOREIGN KEY
    Message         VARCHAR(300)  NOT NULL,
    Type            VARCHAR(20)   NOT NULL DEFAULT 'Info'
                        CHECK (Type IN ('Info','Warning','Alert')),
    IsRead          BIT           NOT NULL DEFAULT 0,
    CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   8. SAVING TIPS LIBRARY  (predefined tip templates — Admin managed)
   ------------------------------------------------------------
   Condition columns let the app pick a tip automatically without
   needing external AI: e.g. TriggerCategory='Food', TriggerType=
   'PercentAbove', TriggerValue=30  ->  show this tip when Food
   crosses 30% of total spending.
   ============================================================ */
CREATE TABLE SavingTips (
    TipId              INT IDENTITY(1,1) PRIMARY KEY,
    TipText            VARCHAR(300)  NOT NULL,
    TriggerCategory    VARCHAR(50)   NULL,                            -- e.g. 'Food', NULL = general tip
    TriggerType        VARCHAR(20)   NULL
                            CHECK (TriggerType IN ('PercentAbove','OverBudget','SavingsBelow',NULL)),
    TriggerValue       DECIMAL(5,2)  NULL,                            -- e.g. 30.00 (%)
    IsActive           BIT           NOT NULL DEFAULT 1,
    CreatedAt          DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

/* ============================================================
   9. BOOKMARKS  (student saves a tip or an insight for later)
   ------------------------------------------------------------
   Polymorphic-style: RefType tells whether RefId points to
   SavingTips.TipId or Insights.InsightId.
   ============================================================ */
CREATE TABLE Bookmarks (
    BookmarkId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId       INT          NOT NULL,                                -- FOREIGN KEY
    RefType      VARCHAR(10)  NOT NULL CHECK (RefType IN ('Tip','Insight')),
    RefId        INT          NOT NULL,                                -- points to TipId or InsightId
    CreatedAt    DATETIME     NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Bookmarks_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_Bookmarks_User_Ref UNIQUE (UserId, RefType, RefId)  -- ek cheez do baar bookmark na ho
);
GO

/* ============================================================
   10. ANNOUNCEMENTS  (Admin -> system-wide announcements)
   ============================================================ */
CREATE TABLE Announcements (
    AnnouncementId  INT IDENTITY(1,1) PRIMARY KEY,
    Title           VARCHAR(150)  NOT NULL,
    Message         VARCHAR(500)  NOT NULL,
    PostedByAdminId INT           NOT NULL,                            -- FOREIGN KEY -> Users (Admin)
    IsActive        BIT           NOT NULL DEFAULT 1,
    CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Announcements_Admin FOREIGN KEY (PostedByAdminId)
        REFERENCES Users(UserId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   10a. PASSWORD RESET TOKENS
   ------------------------------------------------------------
   Supports "Password recovery and reset through email
   verification or a tokenized link" (SRS 1.6).
   ============================================================ */
CREATE TABLE PasswordResetTokens (
    TokenId       INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT           NOT NULL,                              -- FOREIGN KEY
    Token         VARCHAR(255)  NOT NULL UNIQUE,                       -- UNIQUE KEY
    ExpiresAt     DATETIME      NOT NULL,
    IsUsed        BIT           NOT NULL DEFAULT 0,
    CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10b. RECURRING TRANSACTIONS
   ------------------------------------------------------------
   SRS 1.6: "Supports recurring entries (for example, monthly
   allowances and subscription charges)". A background job / app
   logic reads this table and auto-creates rows in Transactions
   on or after NextRunDate.
   ============================================================ */
CREATE TABLE RecurringTransactions (
    RecurringId     INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT           NOT NULL,                            -- FOREIGN KEY
    CategoryId      INT           NOT NULL,                            -- FOREIGN KEY
    Amount          DECIMAL(10,2) NOT NULL CHECK (Amount > 0),
    Type            VARCHAR(10)   NOT NULL CHECK (Type IN ('Income','Expense')),
    Description     VARCHAR(250)  NULL,
    Frequency       VARCHAR(10)   NOT NULL DEFAULT 'Monthly'
                        CHECK (Frequency IN ('Weekly','Monthly','Yearly')),
    NextRunDate     DATE          NOT NULL,
    IsActive        BIT           NOT NULL DEFAULT 1,
    CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_RecurringTransactions_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_RecurringTransactions_Categories FOREIGN KEY (CategoryId)
        REFERENCES Categories(CategoryId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   10c. TIP INTERACTIONS
   ------------------------------------------------------------
   SRS 1.6: "Allows students to dismiss or 'pin' tips they find
   useful". Bookmarks already covers save-for-later; this table
   tracks the per-user, per-tip Dismissed/Pinned state so a
   dismissed tip does not keep reappearing on the dashboard.
   ============================================================ */
CREATE TABLE TipInteractions (
    InteractionId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT       NOT NULL,                                -- FOREIGN KEY
    TipId           INT       NOT NULL,                                -- FOREIGN KEY
    IsDismissed     BIT       NOT NULL DEFAULT 0,
    IsPinned        BIT       NOT NULL DEFAULT 0,
    UpdatedAt       DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_TipInteractions_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_TipInteractions_Tips FOREIGN KEY (TipId)
        REFERENCES SavingTips(TipId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_TipInteractions_User_Tip UNIQUE (UserId, TipId)      -- ek tip ka ek hi state per user
);
GO

/* ============================================================
   10d. CSV IMPORT BATCHES
   ------------------------------------------------------------
   SRS 1.6: "Optional bulk import of historical transactions
   from CSV files" + "batch-categorization suggestions when
   importing CSV transaction history". Keeps an audit trail of
   every import attempt.
   ============================================================ */
CREATE TABLE CsvImportBatches (
    ImportId        INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT           NOT NULL,                            -- FOREIGN KEY
    FileName        VARCHAR(255)  NOT NULL,
    RowsTotal       INT           NOT NULL DEFAULT 0,
    RowsImported    INT           NOT NULL DEFAULT 0,
    RowsFailed      INT           NOT NULL DEFAULT 0,
    Status          VARCHAR(20)   NOT NULL DEFAULT 'Pending'
                        CHECK (Status IN ('Pending','Completed','Failed')),
    ImportedAt      DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_CsvImportBatches_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10e. ACTIVITY LOG
   ------------------------------------------------------------
   SRS "Optional System Intelligence (Advanced UX)":
   "Tracks recently viewed and recently edited transactions
   across sessions" + "Detects and flags unusually large or
   duplicate transactions".
   ============================================================ */
CREATE TABLE ActivityLog (
    LogId           INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT           NOT NULL,                            -- FOREIGN KEY
    TransactionId   INT           NULL,                                -- FOREIGN KEY (nullable, may log non-transaction actions)
    ActionType      VARCHAR(20)   NOT NULL
                        CHECK (ActionType IN ('Viewed','Edited','FlaggedDuplicate','FlaggedLarge')),
    Notes           VARCHAR(250)  NULL,
    CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_ActivityLog_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_ActivityLog_Transactions FOREIGN KEY (TransactionId)
        REFERENCES Transactions(TransactionId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   10f. MONTHLY FORECASTS
   ------------------------------------------------------------
   SRS "Optional System Intelligence": "Forecasts for the
   upcoming month based on historical trends".
   ============================================================ */
CREATE TABLE Forecasts (
    ForecastId          INT IDENTITY(1,1) PRIMARY KEY,
    UserId              INT           NOT NULL,                        -- FOREIGN KEY
    ForMonth            DATE          NOT NULL,                        -- month being predicted
    PredictedIncome     DECIMAL(10,2) NULL,
    PredictedExpense    DECIMAL(10,2) NULL,
    ConfidenceNote      VARCHAR(250)  NULL,                            -- plain-language confidence/explanation
    GeneratedAt         DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Forecasts_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_Forecasts_User_Month UNIQUE (UserId, ForMonth)
);
GO

/* ============================================================
   10g. USER PREFERENCES
   ------------------------------------------------------------
   SRS 1.7 Accessibility: "dark-mode toggle and font-size
   adjustment for accessibility".
   ============================================================ */
CREATE TABLE UserPreferences (
    UserId          INT           NOT NULL PRIMARY KEY,                -- FOREIGN KEY + PK (1:1 with Users)
    DarkMode        BIT           NOT NULL DEFAULT 0,
    FontSize        VARCHAR(10)   NOT NULL DEFAULT 'Medium'
                        CHECK (FontSize IN ('Small','Medium','Large')),
    Currency        VARCHAR(5)    NOT NULL DEFAULT 'PKR',
    UpdatedAt       DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_UserPreferences_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10h. GAMIFICATION — BADGES  (creative addition, own idea)
   ------------------------------------------------------------
   Fun "Smart Spending, Student Style" layer on top of the SRS
   baseline: students earn badges for good habits (e.g. staying
   under budget 3 months running, first savings goal hit).
   ============================================================ */
CREATE TABLE Badges (
    BadgeId       INT IDENTITY(1,1) PRIMARY KEY,
    Name          VARCHAR(100)  NOT NULL UNIQUE,                       -- UNIQUE KEY
    Description   VARCHAR(250)  NOT NULL,
    IconEmoji     VARCHAR(10)   NULL,
    CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE UserBadges (
    UserBadgeId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT       NOT NULL,                                  -- FOREIGN KEY
    BadgeId       INT       NOT NULL,                                  -- FOREIGN KEY
    EarnedAt      DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_UserBadges_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_UserBadges_Badges FOREIGN KEY (BadgeId)
        REFERENCES Badges(BadgeId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_UserBadges_User_Badge UNIQUE (UserId, BadgeId)       -- ek badge ek baar hi milta hai
);
GO

/* ============================================================
   10i. GAMIFICATION — SAVINGS STREAKS  (creative addition)
   ------------------------------------------------------------
   Tracks consecutive months a student stayed within their
   overall budget, to power a "🔥 3-month streak" dashboard
   widget — encourages consistent good habits.
   ============================================================ */
CREATE TABLE SavingsStreaks (
    UserId              INT       NOT NULL PRIMARY KEY,                -- FOREIGN KEY + PK (1:1 with Users)
    CurrentStreakMonths INT       NOT NULL DEFAULT 0,
    LongestStreakMonths INT       NOT NULL DEFAULT 0,
    LastQualifyingMonth DATE      NULL,
    UpdatedAt           DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_SavingsStreaks_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10j. HOSTEL/COMMUNITY CHALLENGES  (creative addition)
   ------------------------------------------------------------
   SRS mentions Campus Coin targets "hostel communities" and
   "campus financial-literacy initiatives" — this lets an Admin
   run a group savings challenge (e.g. "No-Delivery Week") that
   students opt into, adding a social layer beyond the SRS
   baseline.
   ============================================================ */
CREATE TABLE Challenges (
    ChallengeId       INT IDENTITY(1,1) PRIMARY KEY,
    Title             VARCHAR(150)  NOT NULL,
    Description       VARCHAR(500)  NOT NULL,
    StartDate         DATE          NOT NULL,
    EndDate           DATE          NOT NULL,
    CreatedByAdminId  INT           NOT NULL,                          -- FOREIGN KEY -> Users (Admin)
    CreatedAt         DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Challenges_Admin FOREIGN KEY (CreatedByAdminId)
        REFERENCES Users(UserId)
        ON DELETE NO ACTION,

    CONSTRAINT CK_Challenges_Dates CHECK (EndDate >= StartDate)
);
GO

CREATE TABLE ChallengeParticipants (
    ParticipantId   INT IDENTITY(1,1) PRIMARY KEY,
    ChallengeId     INT       NOT NULL,                                -- FOREIGN KEY
    UserId          INT       NOT NULL,                                -- FOREIGN KEY
    HasCompleted    BIT       NOT NULL DEFAULT 0,
    JoinedAt        DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_ChallengeParticipants_Challenges FOREIGN KEY (ChallengeId)
        REFERENCES Challenges(ChallengeId)
        ON DELETE CASCADE,

    CONSTRAINT FK_ChallengeParticipants_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_ChallengeParticipants_Challenge_User UNIQUE (ChallengeId, UserId)
);
GO

/* ============================================================
   10k. COLUMN ADDITIONS  (fixing SRS gaps on existing tables)
   ============================================================ */

-- SRS 1.6: profile editable fields = "name, academic year, monthly
-- allowance baseline, and savings goal". MonthlySavingsGoal already
-- existed; the allowance baseline was missing.
ALTER TABLE Users
    ADD MonthlyAllowanceBaseline DECIMAL(10,2) NOT NULL DEFAULT 0;
GO

-- SRS Admin Control Panel: "User accounts (view, disable, or
-- reset)" — needs a status flag for Admin to disable a student
-- account without deleting their transaction history.
ALTER TABLE Users
    ADD IsActive BIT NOT NULL DEFAULT 1;
GO

-- Docx AI Feature #2 "Automatic Expense Categorization" shows a
-- confidence % next to the AI-suggested category (e.g. "94%").
ALTER TABLE Transactions
    ADD AiConfidenceScore DECIMAL(5,2) NULL;
GO

-- SRS "Optional System Intelligence": duplicate / unusually large
-- transaction flags, kept directly on the row for fast dashboard
-- queries (ActivityLog below keeps the audit trail of *when* it
-- was flagged and why).
ALTER TABLE Transactions
    ADD IsFlagged   BIT           NOT NULL DEFAULT 0,
        FlagReason  VARCHAR(50)   NULL,
        IsDeleted   BIT           NOT NULL DEFAULT 0;              -- soft delete, see 10l
GO

/* ============================================================
   10l. TRANSACTION HISTORY  (audit trail)
   ------------------------------------------------------------
   SRS 1.6: "Edits and deletes transactions while retaining the
   full history." Every edit/delete snapshots the row's previous
   values here before it changes, so nothing is ever truly lost
   even though Transactions.IsDeleted lets the UI hide it.
   No FK to Transactions on purpose — a deleted transaction's
   history row must survive the delete.
   ============================================================ */
CREATE TABLE TransactionHistory (
    HistoryId             INT IDENTITY(1,1) PRIMARY KEY,
    TransactionId         INT           NOT NULL,                      -- references Transactions, not FK-enforced (row may be gone)
    UserId                INT           NOT NULL,                      -- FOREIGN KEY
    ChangeType             VARCHAR(10)   NOT NULL CHECK (ChangeType IN ('Edited','Deleted')),
    PreviousAmount         DECIMAL(10,2) NULL,
    PreviousCategoryId     INT           NULL,
    PreviousType           VARCHAR(10)   NULL,
    PreviousDescription    VARCHAR(250)  NULL,
    PreviousDate           DATE          NULL,
    ChangedAt               DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_TransactionHistory_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10m. CATEGORY CORRECTIONS  (AI "learns from corrections")
   ------------------------------------------------------------
   SRS 1.6: "Learns from the student's corrections over time to
   improve future suggestions." Every time a student overrides
   the AI-suggested category, log Suggested vs Actual here —
   the app can use this table to re-weight future suggestions.
   ============================================================ */
CREATE TABLE CategoryCorrections (
    CorrectionId        INT       IDENTITY(1,1) PRIMARY KEY,
    TransactionId        INT       NOT NULL,                           -- FOREIGN KEY
    UserId               INT       NOT NULL,                           -- FOREIGN KEY
    SuggestedCategoryId  INT       NULL,                                -- FOREIGN KEY (what AI guessed)
    ActualCategoryId     INT       NOT NULL,                            -- FOREIGN KEY (what student chose)
    CreatedAt             DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_CategoryCorrections_Transactions FOREIGN KEY (TransactionId)
        REFERENCES Transactions(TransactionId)
        ON DELETE NO ACTION,

    CONSTRAINT FK_CategoryCorrections_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_CategoryCorrections_Suggested FOREIGN KEY (SuggestedCategoryId)
        REFERENCES Categories(CategoryId)
        ON DELETE NO ACTION,

    CONSTRAINT FK_CategoryCorrections_Actual FOREIGN KEY (ActualCategoryId)
        REFERENCES Categories(CategoryId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   10n. REPORT EXPORTS  (export / email-share log)
   ------------------------------------------------------------
   SRS 1.6: "Exports the monthly report as a PDF or image..." and
   "Optionally exports monthly reports or savings summaries as
   PDFs or shares them by email."
   ============================================================ */
CREATE TABLE ReportExports (
    ExportId          INT IDENTITY(1,1) PRIMARY KEY,
    UserId             INT           NOT NULL,                          -- FOREIGN KEY
    ReportType         VARCHAR(30)   NOT NULL
                            CHECK (ReportType IN ('CategoryWise','IncomeVsExpense','DailySummary','WeeklySummary','SavingsSummary')),
    RangeStart          DATE          NULL,
    RangeEnd            DATE          NULL,
    Format              VARCHAR(10)   NOT NULL CHECK (Format IN ('PDF','Image')),
    SharedWithEmail     VARCHAR(150)  NULL,                              -- filled only if the user chose to email it
    ExportedAt           DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_ReportExports_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10o. SAVINGS GOALS  (creative addition, own idea)
   ------------------------------------------------------------
   Goes beyond the single MonthlySavingsGoal number on Users:
   students can create named goals ("Buy Laptop", target Rs.
   150,000) and track progress toward each one — powers the
   dashboard progress bar + "estimated completion" widget.
   ============================================================ */
CREATE TABLE SavingsGoals (
    GoalId         INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT           NOT NULL,                              -- FOREIGN KEY
    GoalName       VARCHAR(100)  NOT NULL,
    TargetAmount   DECIMAL(10,2) NOT NULL CHECK (TargetAmount > 0),
    CurrentAmount  DECIMAL(10,2) NOT NULL DEFAULT 0,
    TargetDate     DATE          NULL,
    IsAchieved     BIT           NOT NULL DEFAULT 0,
    CreatedAt      DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_SavingsGoals_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10p. FINANCIAL HEALTH SCORES  (creative addition, own idea)
   ------------------------------------------------------------
   Stores the monthly "Financial Health Score" (0-100) with its
   four sub-scores, so the dashboard can show history/trend, not
   just today's number, without recalculating from scratch.
   ============================================================ */
CREATE TABLE FinancialHealthScores (
    ScoreId                INT IDENTITY(1,1) PRIMARY KEY,
    UserId                 INT       NOT NULL,                          -- FOREIGN KEY
    ForMonth               DATE      NOT NULL,
    OverallScore           INT       NOT NULL CHECK (OverallScore BETWEEN 0 AND 100),
    BudgetControlScore     INT       NULL CHECK (BudgetControlScore BETWEEN 0 AND 100),
    SavingRateScore        INT       NULL CHECK (SavingRateScore BETWEEN 0 AND 100),
    ExpenseStabilityScore  INT       NULL CHECK (ExpenseStabilityScore BETWEEN 0 AND 100),
    GoalProgressScore      INT       NULL CHECK (GoalProgressScore BETWEEN 0 AND 100),
    GeneratedAt            DATETIME  NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_FinancialHealthScores_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_FinancialHealthScores_User_Month UNIQUE (UserId, ForMonth)
);
GO

/* ============================================================
   10q. SPENDING PERSONALITY  (creative addition, own idea)
   ------------------------------------------------------------
   Light, fun, non-clinical label derived from spending patterns
   (e.g. "Balanced Saver", "Food Lover") — purely a friendly UI
   touch, recalculated each time and overwritten (1 row/user).
   ============================================================ */
CREATE TABLE SpendingPersonality (
    UserId            INT           NOT NULL PRIMARY KEY,               -- FOREIGN KEY + PK (1:1 with Users)
    PersonalityLabel  VARCHAR(50)   NOT NULL,
    DeterminedAt      DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_SpendingPersonality_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10r. GENERATED TIPS  (per-month, ranked tip instances)
   ------------------------------------------------------------
   SRS 1.6: "Ranks tips by potential savings impact and displays
   the top few on the dashboard." SavingTips holds the reusable
   tip *templates*; this table holds the personalized instance
   the engine picked for a given student in a given month, with
   its estimated Rupee impact and rank — what the dashboard
   actually queries.
   ============================================================ */
CREATE TABLE GeneratedTips (
    GeneratedTipId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT           NOT NULL,                            -- FOREIGN KEY
    TipId            INT           NOT NULL,                            -- FOREIGN KEY -> SavingTips
    ForMonth         DATE          NOT NULL,
    EstimatedImpact  DECIMAL(10,2) NULL,                                 -- estimated Rs. savings if followed
    RankOrder        INT           NULL,                                 -- 1 = highest impact, shown first
    GeneratedAt      DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_GeneratedTips_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE,

    CONSTRAINT FK_GeneratedTips_Tips FOREIGN KEY (TipId)
        REFERENCES SavingTips(TipId)
        ON DELETE NO ACTION
);
GO

/* ============================================================
   10s. SIMULATION LOGS  (creative addition, own idea)
   ------------------------------------------------------------
   Single, flexible log table backing three "what-if" style
   creative features in one place: "Can I Afford It?", "What If
   I Save X?", and the full "Financial What-If Simulator". Each
   run is logged with a short human-readable summary rather than
   a rigid column-per-scenario design, since the three tools ask
   different questions but all just need input → result.
   ============================================================ */
CREATE TABLE SimulationLogs (
    SimulationId     INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT           NOT NULL,                            -- FOREIGN KEY
    SimulationType   VARCHAR(30)   NOT NULL
                          CHECK (SimulationType IN ('CanIAffordIt','WhatIfSave','WhatIfSimulator')),
    InputSummary     VARCHAR(500)  NULL,                                 -- e.g. "Item: Gaming Headset, Price: 12000"
    ResultSummary    VARCHAR(500)  NULL,                                 -- e.g. "Affordable, Rs.500 safe margin left"
    CreatedAt        DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_SimulationLogs_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10t. USER SESSIONS  (secure session management)
   ------------------------------------------------------------
   SRS 1.6: "Secure session management." Persists active login
   sessions server-side so the app can list, expire, or revoke a
   session (e.g. "log out on all devices", or Admin force-logout
   of a disabled account) instead of trusting the cookie alone.
   ============================================================ */
CREATE TABLE UserSessions (
    SessionId     INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT           NOT NULL,                              -- FOREIGN KEY
    SessionToken  VARCHAR(255)  NOT NULL UNIQUE,                       -- UNIQUE KEY
    IpAddress     VARCHAR(45)   NULL,
    CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),
    ExpiresAt     DATETIME      NOT NULL,
    IsRevoked     BIT           NOT NULL DEFAULT 0,

    CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10u. FINANCIAL COACH QUERIES  (creative addition, own idea)
   ------------------------------------------------------------
   Docx "AI Feature #1 — AI Financial Coach" (top-rated: a
   dashboard box where the student asks e.g. "How can I save
   Rs. 5,000 this month?" and gets an answer generated from
   their own transactions/budget — pure C# rule-based logic, no
   paid API needed). This table stores the Q&A history so past
   coaching answers can be reviewed later, same idea as Insights
   but conversational rather than a fixed monthly summary.
   ============================================================ */
CREATE TABLE FinancialCoachQueries (
    QueryId       INT IDENTITY(1,1) PRIMARY KEY,
    UserId        INT           NOT NULL,                              -- FOREIGN KEY
    Question      VARCHAR(300)  NOT NULL,
    AnswerText    VARCHAR(1000) NOT NULL,
    CreatedAt     DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_FinancialCoachQueries_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10v. WISHLIST  (creative addition, own idea)
   ------------------------------------------------------------
   Students can keep a list of things they want to buy, each
   with a priority. Feeds directly into the "Can I Afford It?"
   simulator — instead of typing an item every time, the student
   can just tap a wishlist item and run the check on it.
   ============================================================ */
CREATE TABLE WishlistItems (
    WishlistId       INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT           NOT NULL,                            -- FOREIGN KEY
    ItemName         VARCHAR(150)  NOT NULL,
    EstimatedPrice   DECIMAL(10,2) NOT NULL CHECK (EstimatedPrice > 0),
    Priority         VARCHAR(10)   NOT NULL DEFAULT 'Medium'
                          CHECK (Priority IN ('Low','Medium','High')),
    IsPurchased      BIT           NOT NULL DEFAULT 0,
    CreatedAt        DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_WishlistItems_Users FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10w. SPLIT / SHARED EXPENSES  (creative addition, own idea)
   ------------------------------------------------------------
   Real hostel-life scenario: one student pays for a shared
   dinner/trip/bill and the cost is split with friends. The full
   amount is already logged as a normal Transaction under the
   payer; these two tables record how it was divided, so the
   payer's "true" personal share can be shown separately from
   the amount they fronted. Friends don't need their own
   CampusCoin account — ParticipantName is free text.
   ============================================================ */
CREATE TABLE SharedExpenses (
    SharedExpenseId   INT IDENTITY(1,1) PRIMARY KEY,
    TransactionId     INT           NOT NULL,                           -- FOREIGN KEY -> the payer's logged Transaction
    PayerUserId       INT           NOT NULL,                           -- FOREIGN KEY
    TotalAmount       DECIMAL(10,2) NOT NULL CHECK (TotalAmount > 0),
    Description       VARCHAR(200)  NULL,
    CreatedAt         DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_SharedExpenses_Transactions FOREIGN KEY (TransactionId)
        REFERENCES Transactions(TransactionId)
        ON DELETE NO ACTION,

    CONSTRAINT FK_SharedExpenses_Payer FOREIGN KEY (PayerUserId)
        REFERENCES Users(UserId)
        ON DELETE NO ACTION
);
GO

CREATE TABLE SharedExpenseSplits (
    SplitId           INT IDENTITY(1,1) PRIMARY KEY,
    SharedExpenseId   INT           NOT NULL,                           -- FOREIGN KEY
    ParticipantName   VARCHAR(100)  NOT NULL,                           -- free text: friend may not be a registered user
    ShareAmount       DECIMAL(10,2) NOT NULL CHECK (ShareAmount > 0),
    IsSettled         BIT           NOT NULL DEFAULT 0,                 -- has the friend paid back their share?

    CONSTRAINT FK_SharedExpenseSplits_SharedExpenses FOREIGN KEY (SharedExpenseId)
        REFERENCES SharedExpenses(SharedExpenseId)
        ON DELETE CASCADE
);
GO

/* ============================================================
   10x. CATEGORY BENCHMARKS  (creative addition, own idea)
   ------------------------------------------------------------
   Anonymous, privacy-safe peer comparison: a nightly/monthly job
   aggregates ALL students' spending per category into one row
   per category per month — no individual student is stored or
   identifiable here. The dashboard can then say "Your Food
   spending is 20% above the campus average" without ever
   exposing anyone else's personal transactions.
   ============================================================ */
CREATE TABLE CategoryBenchmarks (
    BenchmarkId     INT IDENTITY(1,1) PRIMARY KEY,
    CategoryId      INT           NOT NULL,                             -- FOREIGN KEY
    ForMonth        DATE          NOT NULL,
    AverageSpend    DECIMAL(10,2) NOT NULL,
    StudentCount    INT           NOT NULL DEFAULT 0,                   -- how many students contributed to the average
    ComputedAt      DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_CategoryBenchmarks_Categories FOREIGN KEY (CategoryId)
        REFERENCES Categories(CategoryId)
        ON DELETE CASCADE,

    CONSTRAINT UQ_CategoryBenchmarks_Category_Month UNIQUE (CategoryId, ForMonth)
);
GO

/* ============================================================
   11. INDEXES  (performance — Dashboard/Reports queries)
   ============================================================ */
CREATE INDEX IX_Transactions_User_Date  ON Transactions(UserId, [Date]);
CREATE INDEX IX_Transactions_Category   ON Transactions(CategoryId);
CREATE INDEX IX_Budgets_User_Month      ON Budgets(UserId, [Month]);
CREATE INDEX IX_Notifications_User      ON Notifications(UserId, IsRead);
CREATE INDEX IX_RecurringTransactions_NextRun ON RecurringTransactions(NextRunDate, IsActive);
CREATE INDEX IX_ActivityLog_User        ON ActivityLog(UserId, CreatedAt);
CREATE INDEX IX_CsvImportBatches_User   ON CsvImportBatches(UserId, Status);
CREATE INDEX IX_Transactions_Flagged    ON Transactions(UserId, IsFlagged, IsDeleted);
CREATE INDEX IX_TransactionHistory_Txn  ON TransactionHistory(TransactionId);
CREATE INDEX IX_SavingsGoals_User       ON SavingsGoals(UserId, IsAchieved);
CREATE INDEX IX_FinancialHealthScores_User ON FinancialHealthScores(UserId, ForMonth);
CREATE INDEX IX_GeneratedTips_User_Month ON GeneratedTips(UserId, ForMonth, RankOrder);
CREATE INDEX IX_SimulationLogs_User     ON SimulationLogs(UserId, SimulationType);
CREATE INDEX IX_UserSessions_Token      ON UserSessions(SessionToken, IsRevoked);
CREATE INDEX IX_FinancialCoachQueries_User ON FinancialCoachQueries(UserId, CreatedAt);
CREATE INDEX IX_WishlistItems_User      ON WishlistItems(UserId, IsPurchased);
CREATE INDEX IX_SharedExpenses_Payer    ON SharedExpenses(PayerUserId);
CREATE INDEX IX_CategoryBenchmarks_Month ON CategoryBenchmarks(ForMonth);
GO

/* ============================================================
   12. SEED DATA — Roles
   ============================================================ */
INSERT INTO Roles (RoleName) VALUES ('Admin'), ('Student');
GO

/* ============================================================
   13. SEED DATA — Default Categories (IsDefault = 1, UserId = NULL)
   ============================================================ */
INSERT INTO Categories (Name, Type, IsDefault, UserId) VALUES
    ('Allowance',      'Income',  1, NULL),
    ('Part-time Job',  'Income',  1, NULL),
    ('Scholarship',    'Income',  1, NULL),
    ('Gift',           'Income',  1, NULL),
    ('Other Income',   'Income',  1, NULL),
    ('Food',           'Expense', 1, NULL),
    ('Transport',      'Expense', 1, NULL),
    ('Hostel/Rent',    'Expense', 1, NULL),
    ('Academics',      'Expense', 1, NULL),
    ('Subscriptions',  'Expense', 1, NULL),
    ('Entertainment',  'Expense', 1, NULL),
    ('Miscellaneous',  'Expense', 1, NULL);
GO

/* ============================================================
   14. SEED DATA — Demo Admin + Student (for testing/demo only)
   NOTE: PasswordHash placeholders only — real passwords come from app seed/config, not this script
   testing ke liye — real app mein hamesha hashed password
   (BCrypt / ASP.NET Identity hasher) store hoga.
   ============================================================ */
INSERT INTO Users (FullName, Email, PasswordHash, AcademicYear, MonthlySavingsGoal, RoleId)
VALUES
    ('System Admin', 'admin@campuscoin.com', 'SET_BY_APPLICATION_SEED', NULL, 0,
        (SELECT RoleId FROM Roles WHERE RoleName='Admin')),
    ('Ayesha Khan',  'ayesha@campus.edu',    'SET_BY_APPLICATION_SEED', '3rd Year', 50000,
        (SELECT RoleId FROM Roles WHERE RoleName='Student'));
GO

/* ============================================================
   15. SEED DATA — Saving Tips Library (auto-suggested, no AI needed)
   ============================================================ */
INSERT INTO SavingTips (TipText, TriggerCategory, TriggerType, TriggerValue) VALUES
    ('Your Food spending is above 30% of total expenses — try a weekly cap to control it.', 'Food', 'PercentAbove', 30.00),
    ('Entertainment expenses have exceeded your planned budget this month.', 'Entertainment', 'OverBudget', NULL),
    ('Your savings rate is below 10% — consider setting a weekly spending limit.', NULL, 'SavingsBelow', 10.00),
    ('Transport expenses increased compared with last month — check for recurring fares you can reduce.', 'Transport', 'PercentAbove', 20.00);
GO

/* ============================================================
   16. SAMPLE TRANSACTIONS (for the demo student, UserId = 2)
   ============================================================ */
INSERT INTO Transactions (UserId, CategoryId, Amount, Type, Description, [Date])
VALUES
    (2, (SELECT CategoryId FROM Categories WHERE Name='Allowance'), 24500, 'Income',  'Monthly allowance', '2026-09-16'),
    (2, (SELECT CategoryId FROM Categories WHERE Name='Food'),        450, 'Expense', 'Campus Cafe',       '2026-09-15'),
    (2, (SELECT CategoryId FROM Categories WHERE Name='Transport'),  1200, 'Expense', 'Transport pass',    '2026-09-12'),
    (2, (SELECT CategoryId FROM Categories WHERE Name='Gift'),       5000, 'Income',  'Eid gift',          '2026-09-08');
GO

/* ============================================================
   17. SAMPLE BUDGET (for the demo student)
   ============================================================ */
INSERT INTO Budgets (UserId, CategoryId, [Month], LimitAmount)
VALUES (2, (SELECT CategoryId FROM Categories WHERE Name='Food'), '2026-09-01', 5000);
GO

/* ============================================================
   18. SAMPLE NOTIFICATION + ANNOUNCEMENT (demo)
   ============================================================ */
INSERT INTO Notifications (UserId, Message, Type)
VALUES (2, 'Your Food budget has reached 72% of its limit this month.', 'Warning');
GO

INSERT INTO Announcements (Title, Message, PostedByAdminId)
VALUES ('Welcome to CampusCoin!', 'Track your allowance, set budgets, and get smart saving tips — all in one place.',
        (SELECT UserId FROM Users WHERE Email='admin@campuscoin.com'));
GO

/* ============================================================
   19. SEED DATA — Badges  (Admin managed, gamification)
   ============================================================ */
INSERT INTO Badges (Name, Description, IconEmoji) VALUES
    ('First Step',        'Logged your very first transaction.',                         '🪙'),
    ('Budget Boss',        'Stayed within budget for an entire month.',                   '🎯'),
    ('3-Month Streak',     'Stayed within your overall budget 3 months in a row.',         '🔥'),
    ('Savings Star',       'Hit a monthly savings goal for the first time.',               '⭐'),
    ('Tip Explorer',       'Bookmarked 5 saving tips.',                                    '📌'),
    ('First Saver',        'Saved your first Rs. 5,000.',                                  '💰'),
    ('Goal Setter',        'Created your first savings goal.',                             '🎯'),
    ('7-Day Tracker',      'Recorded expenses for 7 consecutive days.',                    '🔥');
GO

/* ============================================================
   20. SEED DATA — Demo Preferences, Streak, and Challenge
   ============================================================ */
INSERT INTO UserPreferences (UserId, DarkMode, FontSize, Currency)
VALUES (2, 0, 'Medium', 'PKR');
GO

INSERT INTO SavingsStreaks (UserId, CurrentStreakMonths, LongestStreakMonths, LastQualifyingMonth)
VALUES (2, 1, 1, '2026-09-01');
GO

INSERT INTO UserBadges (UserId, BadgeId)
VALUES (2, (SELECT BadgeId FROM Badges WHERE Name = 'First Step'));
GO

INSERT INTO Challenges (Title, Description, StartDate, EndDate, CreatedByAdminId)
VALUES (
    'No-Delivery Week',
    'Skip food delivery apps for 7 days and log what you save instead.',
    '2026-09-22', '2026-09-28',
    (SELECT UserId FROM Users WHERE Email = 'admin@campuscoin.com')
);
GO

INSERT INTO ChallengeParticipants (ChallengeId, UserId)
VALUES (
    (SELECT ChallengeId FROM Challenges WHERE Title = 'No-Delivery Week'),
    2
);
GO

/* ============================================================
   21. SEED DATA — Demo Allowance Baseline
   ============================================================ */
UPDATE Users
SET MonthlyAllowanceBaseline = 24500
WHERE Email = 'ayesha@campus.edu';
GO

/* ============================================================
   22. SEED DATA — Savings Goal, Health Score, Personality,
                    Generated Tip, Simulation Log (demo student)
   ============================================================ */
INSERT INTO SavingsGoals (UserId, GoalName, TargetAmount, CurrentAmount, TargetDate)
VALUES (2, 'Buy Laptop', 150000, 65000, '2027-02-01');
GO

INSERT INTO FinancialHealthScores
    (UserId, ForMonth, OverallScore, BudgetControlScore, SavingRateScore, ExpenseStabilityScore, GoalProgressScore)
VALUES (2, '2026-09-01', 82, 90, 80, 75, 85);
GO

INSERT INTO SpendingPersonality (UserId, PersonalityLabel)
VALUES (2, 'Balanced Saver');
GO

INSERT INTO GeneratedTips (UserId, TipId, ForMonth, EstimatedImpact, RankOrder)
VALUES (
    2,
    (SELECT TipId FROM SavingTips WHERE TriggerCategory = 'Food'),
    '2026-09-01', 2000.00, 1
);
GO

INSERT INTO SimulationLogs (UserId, SimulationType, InputSummary, ResultSummary)
VALUES (
    2, 'CanIAffordIt',
    'Item: Gaming Headset, Price: Rs. 12,000',
    'Affordable — Rs. 12,500 safe spending available this month.'
);
GO

INSERT INTO UserBadges (UserId, BadgeId)
VALUES (2, (SELECT BadgeId FROM Badges WHERE Name = 'Goal Setter'));
GO

/* ============================================================
   23. SEED DATA — Session + AI Financial Coach Query (demo)
   ============================================================ */
INSERT INTO UserSessions (UserId, SessionToken, IpAddress, ExpiresAt)
VALUES (2, 'DEMO_SESSION_TOKEN_0001', '127.0.0.1', DATEADD(HOUR, 4, GETDATE()));
GO

INSERT INTO FinancialCoachQueries (UserId, Question, AnswerText)
VALUES (
    2,
    'How can I save Rs. 5,000 this month?',
    'Your spending analysis shows Food and Transport are your two largest flexible expenses. Reducing Food by Rs. 2,000 and Transport by Rs. 500 could move you closer to your Rs. 5,000 savings target.'
);
GO

/* ============================================================
   24. SEED DATA — Wishlist, Shared Expense, Category Benchmark
   ============================================================ */
INSERT INTO WishlistItems (UserId, ItemName, EstimatedPrice, Priority)
VALUES (2, 'Gaming Headset', 12000, 'Medium');
GO

-- Demo: Ayesha paid Rs. 2,000 for a group dinner (already logged
-- as a Food transaction), split 4 ways with 3 hostel friends.
INSERT INTO Transactions (UserId, CategoryId, Amount, Type, Description, [Date])
VALUES (2, (SELECT CategoryId FROM Categories WHERE Name='Food'), 2000, 'Expense', 'Group dinner - hostel friends', '2026-09-18');
GO

INSERT INTO SharedExpenses (TransactionId, PayerUserId, TotalAmount, Description)
VALUES (
    (SELECT TOP 1 TransactionId FROM Transactions WHERE Description = 'Group dinner - hostel friends' ORDER BY TransactionId DESC),
    2, 2000, 'Group dinner split with hostel friends'
);
GO

INSERT INTO SharedExpenseSplits (SharedExpenseId, ParticipantName, ShareAmount, IsSettled)
VALUES
    ((SELECT TOP 1 SharedExpenseId FROM SharedExpenses ORDER BY SharedExpenseId DESC), 'Ayesha (me)', 500, 1),
    ((SELECT TOP 1 SharedExpenseId FROM SharedExpenses ORDER BY SharedExpenseId DESC), 'Sana', 500, 1),
    ((SELECT TOP 1 SharedExpenseId FROM SharedExpenses ORDER BY SharedExpenseId DESC), 'Hira', 500, 0),
    ((SELECT TOP 1 SharedExpenseId FROM SharedExpenses ORDER BY SharedExpenseId DESC), 'Fatima', 500, 0);
GO

INSERT INTO CategoryBenchmarks (CategoryId, ForMonth, AverageSpend, StudentCount)
VALUES ((SELECT CategoryId FROM Categories WHERE Name='Food'), '2026-09-01', 6800.00, 125);
GO

/* ============================================================
   DONE. Quick sanity checks below (optional — run separately)
   ============================================================ */
-- SELECT * FROM Users;
-- SELECT * FROM Categories;
-- SELECT * FROM Transactions;
-- SELECT * FROM Budgets;
-- SELECT * FROM Badges;
-- SELECT * FROM UserBadges;
-- SELECT * FROM Challenges;
-- SELECT * FROM SavingsGoals;
-- SELECT * FROM FinancialHealthScores;
-- SELECT * FROM SpendingPersonality;
-- SELECT * FROM GeneratedTips;
-- SELECT * FROM SimulationLogs;
