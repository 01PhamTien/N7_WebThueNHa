using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web2_ThueNha.Migrations
{
    /// <inheritdoc />
    public partial class ThemDuyetThanhToanThuCong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedById",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReviewedById",
                table: "Payments",
                column: "ReviewedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_ReviewedById",
                table: "Payments",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_Contract_Deposit",
                table: "Invoices",
                columns: new[] { "ContractId", "InvoiceType" },
                unique: true,
                filter: "[InvoiceType] = 0 AND [Status] <> 5");

            migrationBuilder.CreateIndex(
                name: "UX_Invoices_Contract_MonthlyRent_Period",
                table: "Invoices",
                columns: new[] { "ContractId", "InvoiceType", "PeriodStart", "PeriodEnd" },
                unique: true,
                filter: "[InvoiceType] = 1 AND [PeriodStart] IS NOT NULL AND [PeriodEnd] IS NOT NULL AND [Status] <> 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Invoices_Contract_Deposit",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "UX_Invoices_Contract_MonthlyRent_Period",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_ReviewedById",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReviewedById",
                table: "Payments");

            migrationBuilder.DropColumn(name: "ReviewedById", table: "Payments");
            migrationBuilder.DropColumn(name: "ReviewedAt", table: "Payments");
        }
    }
}
