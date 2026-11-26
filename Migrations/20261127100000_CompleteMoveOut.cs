using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Web2_ThueNha.Migrations;
public partial class CompleteMoveOut : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.AddColumn<DateTime>(name:"HandoverAt",table:"MoveOutRequests",type:"datetime2",nullable:true);
  m.AddColumn<string>(name:"InspectionNotes",table:"MoveOutRequests",type:"nvarchar(max)",nullable:true);
  m.AddColumn<decimal>(name:"ElectricReading",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"WaterReading",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"RentDue",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"RentPaid",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"OtherDue",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"OtherPaid",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"RefundAmount",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"AdditionalAmount",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"DepositPaid",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"Outstanding",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<decimal>(name:"ExtraCharges",table:"MoveOutRequests",type:"decimal(18,2)",nullable:true);
  m.AddColumn<string>(name:"SettlementNotes",table:"MoveOutRequests",type:"nvarchar(max)",nullable:true);
  m.AddColumn<DateTime>(name:"TenantConfirmedAt",table:"MoveOutRequests",type:"datetime2",nullable:true);
  m.AddColumn<DateTime>(name:"LandlordConfirmedAt",table:"MoveOutRequests",type:"datetime2",nullable:true);
  m.AddColumn<string>(name:"TransferReference",table:"MoveOutRequests",type:"nvarchar(max)",nullable:true);
 }
 protected override void Down(MigrationBuilder m)
 {
  m.DropColumn(name:"HandoverAt",table:"MoveOutRequests");
  m.DropColumn(name:"InspectionNotes",table:"MoveOutRequests");
  m.DropColumn(name:"ElectricReading",table:"MoveOutRequests");
  m.DropColumn(name:"WaterReading",table:"MoveOutRequests");
  m.DropColumn(name:"RentDue",table:"MoveOutRequests");
  m.DropColumn(name:"RentPaid",table:"MoveOutRequests");
  m.DropColumn(name:"OtherDue",table:"MoveOutRequests");
  m.DropColumn(name:"OtherPaid",table:"MoveOutRequests");
  m.DropColumn(name:"RefundAmount",table:"MoveOutRequests");
  m.DropColumn(name:"AdditionalAmount",table:"MoveOutRequests");
  m.DropColumn(name:"DepositPaid",table:"MoveOutRequests");
  m.DropColumn(name:"Outstanding",table:"MoveOutRequests");
  m.DropColumn(name:"ExtraCharges",table:"MoveOutRequests");
  m.DropColumn(name:"SettlementNotes",table:"MoveOutRequests");
  m.DropColumn(name:"TenantConfirmedAt",table:"MoveOutRequests");
  m.DropColumn(name:"LandlordConfirmedAt",table:"MoveOutRequests");
  m.DropColumn(name:"TransferReference",table:"MoveOutRequests");
 }
}
