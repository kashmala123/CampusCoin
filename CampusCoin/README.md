# CampusCoin — Smart Spending, Student Style

Full-stack student budget & expense tracker (ASP.NET Core 8 + SQL Server).

## Requirements

- .NET 8 SDK
- SQL Server (LocalDB, Express, or full)
- Visual Studio 2022 / VS Code / Rider (optional)

## Installation

1. Extract the project folder.
2. Open `CampusCoin.csproj` (or the folder in your IDE).
3. Configure secrets (User Secrets or environment) — connection string, optional Email / Gemini, and `Seed:DemoAdminPassword`. Do not commit real secrets to GitHub.
4. Run:

```bash
dotnet restore
dotnet run
```

5. Open the URL shown in the console.

On first run the database is created and seeded automatically.

## Seeded accounts (passwords never committed)

No default passwords are stored in source code. On first run, users are created **only** if you provide passwords via User Secrets or environment variables:

| Role       | Email                          | Config key / env var |
|------------|--------------------------------|----------------------|
| Admin      | admin@campuscoin.com           | `Seed:AdminPassword` / `CAMPUSCOIN_ADMIN_PASSWORD` |
| Demo Admin | demo.admin@campuscoin-demo.com | `Seed:DemoAdminPassword` / `CAMPUSCOIN_DEMO_ADMIN_PASSWORD` |
| Student    | alex@campuscoin.com            | `Seed:StudentPassword` / `CAMPUSCOIN_STUDENT_PASSWORD` |
| Student    | ayesha@campus.edu              | same as Student above |

### Demo Admin (read-only portfolio account)

- Logs in at `/Auth/AdminLogin` and can **view** the Admin dashboard and all read-only Admin pages/APIs.
- **Cannot** create, edit, delete, reset passwords, or change settings (enforced server-side with 403).

### Local setup (User Secrets)

```bash
dotnet user-secrets init --project CampusCoin
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_SQL_CONNECTION" --project CampusCoin
dotnet user-secrets set "Seed:AdminPassword" "YOUR_ADMIN_PASSWORD" --project CampusCoin
dotnet user-secrets set "Seed:DemoAdminPassword" "YOUR_DEMO_PASSWORD" --project CampusCoin
dotnet user-secrets set "Seed:StudentPassword" "YOUR_STUDENT_PASSWORD" --project CampusCoin
# Optional:
# dotnet user-secrets set "Gemini:ApiKey" "..." --project CampusCoin
# Email:* settings for contact/newsletter/password-reset
```

### Secrets (do not commit real values)

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:DefaultConnection` | SQL Server |
| `Gemini:ApiKey` | Google Gemini (optional) |
| `Email:*` | SMTP for contact / password reset |
| `Seed:AdminPassword` | Real Admin seed password |
| `Seed:DemoAdminPassword` | Demo Admin seed password |
| `Seed:StudentPassword` | Demo student seed password |

## SRS coverage (100%)

- Auth: student register/login, admin login, sessions, password reset
- Profile: name, academic year, allowance baseline, savings goal
- Categories (income/expense) + personal CRUD
- Transactions: add/edit/delete, recurring, history, anomaly flags
- CSV import + **batch category suggestions**
- **Category suggestion while typing** (keyword rules + past history + learning)
- Budgets + progress + in-app notifications when nearing/exceeding
- Saving tips with **bookmark/pin** and **dismiss**
- AI coach + monthly insights (advisory) + next-month forecast
- Reports: category-wise, income-vs-expense (6 months), **daily + weekly** summaries, filters, CSV export, **PDF/Image export**, printable HTML report, **email share**
- Admin: users, categories, stats, announcements, events, vouchers, homepage CMS
- Dark mode + **font size (A- / A / A+)** + breadcrumbs
- Public homepage with **sitemap**
- **EXTRA (unique): Roommate Bill Split** — split hostel/canteen costs, track who settled; linked on all student pages, dashboard widget, Transactions shortcut, homepage sitemap

## Key URLs

| Page | URL |
|------|-----|
| Public home | `/` |
| Student login | `/Account/Login` |
| Admin login | `/Auth/AdminLogin` |
| Dashboard | `/Dashboard` |
| Admin panel | `/Admin` |
| Homepage CMS | `/Admin/HomepageCms` |

## Notes

- No bank linking or real payments (per SRS).
- AI text is advisory only.
- After install: Admin → Homepage CMS → **Init / Repair CMS**.
- Gemini API key is optional (local AI fallback works without it).

## Submission checklist

- [x] Working application matching SRS
- [x] README + credentials
- [x] SQL script (`Database/CampusCoin_Database.sql`)
- [ ] Project report (no source code) — prepare separately
- [ ] Demo video (.mp4) — record separately

CampusCoin · NextGen BudgetBee · End-to-End Web Solutions


## Database / deployment

On startup the app runs **EF Core migrations** (`Database.MigrateAsync()`), then seeds default data if empty.

- **Local / mentor:** just set the connection string and press Start (or `dotnet run`). No manual `Update-Database` required.
- **Deploy:** point `ConnectionStrings:DefaultConnection` at the server SQL database. First run creates/updates schema automatically.
- If an old database was created with `EnsureCreated` only, either use a **new empty database** on the server, or drop the old one once so migrations can apply cleanly.
