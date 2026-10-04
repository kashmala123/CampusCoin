using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoin.Migrations
{
    /// <inheritdoc />
    public partial class WidenWalletPinColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WalletPin may already exist as NVARCHAR(20) from DbInitializer self-check,
            // or be missing on older DBs. Widen/add safely without data loss.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Users', N'WalletPin') IS NULL
        ALTER TABLE dbo.Users ADD WalletPin NVARCHAR(256) NULL;
    ELSE
        ALTER TABLE dbo.Users ALTER COLUMN WalletPin NVARCHAR(256) NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not shrink the column on Down — existing Identity hashes would not fit nvarchar(20).
            // No-op to avoid data truncation.
        }
    }
}
