using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Web2_ThueNha.Migrations;
public partial class AddMoveOutRequests:Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.CreateTable(name:"MoveOutRequests",columns:table=>new{
   Id=table.Column<Guid>(type:"uniqueidentifier",nullable:false),
   ContractId=table.Column<Guid>(type:"uniqueidentifier",nullable:false),
   TenantId=table.Column<Guid>(type:"uniqueidentifier",nullable:false),
   LandlordId=table.Column<Guid>(type:"uniqueidentifier",nullable:false),
   RequestedMoveOutDate=table.Column<DateOnly>(type:"date",nullable:false),
   Reason=table.Column<string>(type:"nvarchar(1000)",maxLength:1000,nullable:false),
   TenantNotes=table.Column<string>(type:"nvarchar(2000)",maxLength:2000,nullable:true),
   LandlordNotes=table.Column<string>(type:"nvarchar(2000)",maxLength:2000,nullable:true),
   CreatedAt=table.Column<DateTime>(type:"datetime2",nullable:false),
   UpdatedAt=table.Column<DateTime>(type:"datetime2",nullable:true),
   CompletedAt=table.Column<DateTime>(type:"datetime2",nullable:true),
   Status=table.Column<int>(type:"int",nullable:false)
  },constraints:table=>{
   table.PrimaryKey("PK_MoveOutRequests",x=>x.Id);
   table.ForeignKey("FK_MoveOutRequests_RentalContracts_ContractId","MoveOutRequests","RentalContracts","Id",onDelete:ReferentialAction.Restrict);
   table.ForeignKey("FK_MoveOutRequests_Users_TenantId","MoveOutRequests","Users","Id",onDelete:ReferentialAction.Restrict);
   table.ForeignKey("FK_MoveOutRequests_Users_LandlordId","MoveOutRequests","Users","Id",onDelete:ReferentialAction.Restrict);
  });
  m.CreateIndex("IX_MoveOutRequests_ContractId","MoveOutRequests","ContractId");
  m.CreateIndex("IX_MoveOutRequests_TenantId_CreatedAt","MoveOutRequests",new[]{"TenantId","CreatedAt"});
  m.CreateIndex("IX_MoveOutRequests_LandlordId_CreatedAt","MoveOutRequests",new[]{"LandlordId","CreatedAt"});
 }
 protected override void Down(MigrationBuilder m)=>m.DropTable("MoveOutRequests");
}