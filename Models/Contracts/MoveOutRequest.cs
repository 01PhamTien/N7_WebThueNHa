using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Models.Identity;
namespace Web2_ThueNha.Models.Contracts;
public enum MoveOutStatus { Pending=0, Approved=1, Rejected=2, Cancelled=3, InspectionScheduled=4, AwaitingSettlement=5, Completed=6 }
public sealed class MoveOutRequest
{
 public Guid Id {get;set;}=Guid.NewGuid();
 public Guid ContractId {get;set;}
 public Guid TenantId {get;set;}
 public Guid LandlordId {get;set;}
 public DateOnly RequestedMoveOutDate {get;set;}
 public string Reason {get;set;}=string.Empty;
 public string? TenantNotes {get;set;}
 public string? LandlordNotes {get;set;}
 public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
 public DateTime? UpdatedAt {get;set;}
 public DateTime? CompletedAt {get;set;}
 public MoveOutStatus Status {get;set;}=MoveOutStatus.Pending;
 public DateTime? HandoverAt {get;set;}
 public string? InspectionNotes {get;set;}
 public string? PhotoLinks {get;set;}
 public decimal? ElectricReading {get;set;}
 public decimal? WaterReading {get;set;}
 public decimal? DepositPaid {get;set;}
 public decimal? Outstanding {get;set;}
 public decimal? ExtraCharges {get;set;}
 public string? SettlementNotes {get;set;}
 public DateTime? TenantConfirmedAt {get;set;}
 public DateTime? LandlordConfirmedAt {get;set;}
 public string? TransferReference {get;set;}
 public RentalContract Contract {get;set;}=null!;
 public User Tenant {get;set;}=null!;
 public User Landlord {get;set;}=null!;
}