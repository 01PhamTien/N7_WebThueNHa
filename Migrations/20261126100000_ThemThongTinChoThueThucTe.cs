using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Web2_ThueNha.Data;

#nullable disable

namespace Web2_ThueNha.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261126100000_ThemThongTinChoThueThucTe")]
public partial class ThemThongTinChoThueThucTe : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DepositAmount", table: "Properties", type: "decimal(18,2)",
            precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<int>(
            name: "MaxOccupants", table: "Properties", type: "int",
            nullable: false, defaultValue: 2);
        migrationBuilder.AddColumn<string>(
            name: "FurnishingDescription", table: "Properties", type: "nvarchar(1000)",
            maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "AdditionalFeesDescription", table: "Properties", type: "nvarchar(1000)",
            maxLength: 1000, nullable: true);

        migrationBuilder.Sql(@"
            UPDATE Properties
            SET DepositAmount = PricePerMonth,
                MaxOccupants = CASE
                    WHEN PropertyTypeId = 1 THEN CASE WHEN Area >= 30 THEN 3 ELSE 2 END
                    WHEN PropertyTypeId = 2 THEN CASE WHEN Bedrooms >= 2 THEN 5 ELSE 3 END
                    WHEN PropertyTypeId = 3 THEN CASE WHEN Bedrooms >= 3 THEN 6 ELSE 4 END
                    WHEN PropertyTypeId = 4 THEN 2
                    ELSE 1
                END;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DepositAmount", table: "Properties");
        migrationBuilder.DropColumn(name: "MaxOccupants", table: "Properties");
        migrationBuilder.DropColumn(name: "FurnishingDescription", table: "Properties");
        migrationBuilder.DropColumn(name: "AdditionalFeesDescription", table: "Properties");
    }
}
