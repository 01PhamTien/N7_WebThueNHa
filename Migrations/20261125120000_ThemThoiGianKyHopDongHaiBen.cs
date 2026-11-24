using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web2_ThueNha.Migrations
{
    /// <inheritdoc />
    public partial class ThemThoiGianKyHopDongHaiBen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LandlordSignedAt",
                table: "RentalContracts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TenantSignedAt",
                table: "RentalContracts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LandlordSignedAt", table: "RentalContracts");
            migrationBuilder.DropColumn(name: "TenantSignedAt", table: "RentalContracts");
        }
    }
}
