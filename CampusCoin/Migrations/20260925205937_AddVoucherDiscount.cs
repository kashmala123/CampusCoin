using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoin.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "FeeVouchers",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discount",
                table: "FeeVouchers");
        }
    }
}
