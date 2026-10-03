using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoin.Migrations
{
    public partial class AddWalletLedgers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'dbo.WalletLedgers', N'U') IS NOT NULL DROP TABLE dbo.WalletLedgers;");
        }
    }
}
