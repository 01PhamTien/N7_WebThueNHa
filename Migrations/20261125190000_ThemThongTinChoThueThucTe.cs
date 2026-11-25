using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Web2_ThueNha.Data;

#nullable disable

namespace Web2_ThueNha.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261125190000_ThemThongTinChoThueThucTe")]
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
                END,
                FurnishingDescription = CASE PropertyTypeId
                    WHEN 1 THEN N'Giường, nệm, tủ quần áo; trang bị cơ bản theo hiện trạng phòng.'
                    WHEN 2 THEN N'Bếp, tủ bếp, máy nước nóng; nội thất chi tiết theo hiện trạng căn hộ.'
                    WHEN 3 THEN N'Tủ bếp, rèm cửa và thiết bị vệ sinh; nội thất chi tiết theo hiện trạng nhà.'
                    WHEN 4 THEN N'Giường, tủ quần áo, máy lạnh và khu bếp nhỏ.'
                    ELSE N'Mặt bằng bàn giao cơ bản, khách thuê tự bố trí thiết bị kinh doanh.'
                END,
                AdditionalFeesDescription = N'Điện, nước, gửi xe và phí dịch vụ (nếu có) tính riêng theo mức giá và cách tính ghi trong hợp đồng.';
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
