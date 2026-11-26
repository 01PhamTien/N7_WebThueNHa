using System.Security.Claims;
using System.Data;
using Web2_ThueNha.Models.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models.Contracts;
namespace Web2_ThueNha.Controllers;
[ApiController]
[Route("api/move-outs")]
[Authorize(Roles="Tenant,Landlord")]
public sealed class MoveOutController(AppDbContext db):ControllerBase
{
 bool UserId(out Guid id)=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out id);
 [HttpGet]
 public async Task<IActionResult> List(CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  var isLandlord=User.IsInRole("Landlord");
  var q=db.MoveOutRequests.AsNoTracking().Where(x=>isLandlord?x.LandlordId==uid:x.TenantId==uid);
  var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Select(x=>new {x.Id,x.ContractId,x.Contract.ContractCode,PropertyTitle=x.Contract.Property.Title,x.RequestedMoveOutDate,x.Reason,x.TenantNotes,x.LandlordNotes,Status=x.Status.ToString(),x.CreatedAt,x.CompletedAt,x.HandoverAt,x.InspectionNotes,x.RentDue,x.RentPaid,x.OtherDue,x.OtherPaid,x.RefundAmount,x.AdditionalAmount,x.ElectricReading,x.WaterReading,x.DepositPaid,x.Outstanding,x.ExtraCharges,x.SettlementNotes,x.TenantConfirmedAt,x.LandlordConfirmedAt,x.TransferReference,TenantName=x.Tenant.FullName,LandlordName=x.Landlord.FullName}).ToListAsync(ct);
  return Ok(rows);
 }
 [HttpPost]
 [Authorize(Roles="Tenant")]
 public async Task<IActionResult> Create(CreateMoveOutRequest body,CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  if(body.Reason.Trim().Length is <3 or >1000 || body.TenantNotes?.Length>2000)return BadRequest(new{message="Lý do hoặc ghi chú không hợp lệ."});
  var today=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
  if(body.RequestedMoveOutDate<today)return BadRequest(new{message="Ngày dự kiến trả nhà không được ở quá khứ."});
  await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var contract=await db.RentalContracts.Include(x=>x.Property).SingleOrDefaultAsync(x=>x.Id==body.ContractId&&x.TenantId==uid,ct);
  if(contract is null)return NotFound(new{message="Không tìm thấy hợp đồng."});
  if(contract.Status!=RentalContractStatus.Active)return Conflict(new{message="Chỉ hợp đồng đang hiệu lực mới được gửi yêu cầu trả nhà."});
  if(body.RequestedMoveOutDate<contract.StartDate)return BadRequest(new{message="Ngày trả nhà không được trước ngày bắt đầu hợp đồng."});
  var exists=await db.MoveOutRequests.AnyAsync(x=>x.ContractId==contract.Id&&x.Status!=MoveOutStatus.Rejected&&x.Status!=MoveOutStatus.Cancelled&&x.Status!=MoveOutStatus.Completed,ct);
  if(exists)return Conflict(new{message="Hợp đồng đã có yêu cầu trả nhà đang xử lý."});
  var row=new MoveOutRequest{ContractId=contract.Id,TenantId=uid,LandlordId=contract.LandlordId,RequestedMoveOutDate=body.RequestedMoveOutDate,Reason=body.Reason.Trim(),TenantNotes=body.TenantNotes?.Trim()};
  db.MoveOutRequests.Add(row); Notify(row.LandlordId,row,"Yêu cầu trả nhà mới"); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
  return CreatedAtAction(nameof(List),new{id=row.Id},new{id=row.Id,status=row.Status.ToString()});
 }
 [HttpPost("{id:guid}/landlord-response")]
 [Authorize(Roles="Landlord")]
 public async Task<IActionResult> Respond(Guid id,LandlordMoveOutResponse body,CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  var row=await db.MoveOutRequests.SingleOrDefaultAsync(x=>x.Id==id&&x.LandlordId==uid,ct);
  if(row is null)return NotFound();
  if(row.Status!=MoveOutStatus.Pending)return Conflict(new{message="Yêu cầu không còn ở trạng thái chờ xử lý."});
  if(body.Approve){row.Status=MoveOutStatus.Approved;row.HandoverAt=RequestedHandoverUtc(row);}else{row.Status=MoveOutStatus.Rejected;}
  if(body.Notes?.Length>2000)return BadRequest(); row.LandlordNotes=body.Notes?.Trim();row.UpdatedAt=DateTime.UtcNow;Notify(row.TenantId,row,body.Approve?"Yêu cầu trả nhà đã được duyệt":"Yêu cầu trả nhà bị từ chối");await db.SaveChangesAsync(ct);return Ok(new{status=row.Status.ToString()});
 }
 [HttpPost("{id:guid}/cancel")]
 [Authorize(Roles="Tenant")]
 public async Task<IActionResult> Cancel(Guid id,CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  var row=await db.MoveOutRequests.SingleOrDefaultAsync(x=>x.Id==id&&x.TenantId==uid,ct);
  if(row is null)return NotFound();
  if(row.Status is not (MoveOutStatus.Pending or MoveOutStatus.Approved))return Conflict(new{message="Không thể hủy yêu cầu ở trạng thái hiện tại."});
  row.Status=MoveOutStatus.Cancelled;row.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{status=row.Status.ToString()});
 }
 void Notify(Guid recipient,MoveOutRequest row,string title)=>db.Notifications.Add(new Notification{UserId=recipient,Title=title,Content="Vui lòng kiểm tra mục trả nhà để xem chi tiết.",NotificationType=NotificationType.Contract,ReferenceId=row.ContractId});
 static DateOnly Today()=>DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
 // Existing signed/issued settlement records retain their agreed date; new records use the tenant's requested date.
 static DateOnly SettlementDate(MoveOutRequest row)=>row.HandoverAt.HasValue && row.Status==MoveOutStatus.AwaitingSettlement
  ? DateOnly.FromDateTime(row.HandoverAt.Value.AddHours(7))
  : row.RequestedMoveOutDate;
 static DateTime RequestedHandoverUtc(MoveOutRequest row)=>DateTime.SpecifyKind(row.RequestedMoveOutDate.ToDateTime(TimeOnly.MinValue).AddHours(-7),DateTimeKind.Utc);
 static decimal Paid(Web2_ThueNha.Models.Payments.Invoice i)=>i.Payments.Where(p=>p.Status==PaymentStatus.Succeeded).Sum(p=>p.Amount);
 static (decimal RentDue,decimal RentPaid,decimal OtherDue,decimal OtherPaid,decimal DepositPaid,decimal Balance) Calculate(MoveOutRequest row,List<Web2_ThueNha.Models.Payments.Invoice> invoices,decimal extras)
 {
  var end=SettlementDate(row);
  if(end>row.Contract.EndDate)end=row.Contract.EndDate;
  var rentDue=end<=row.Contract.StartDate?row.Contract.MonthlyRent:RentalPricing.Calculate(row.Contract.MonthlyRent,row.Contract.StartDate,end).TotalAmount;
  var rentPaid=invoices.Where(i=>i.InvoiceType==InvoiceType.MonthlyRent).Sum(Paid);
  var otherDue=invoices.Where(i=>i.InvoiceType==InvoiceType.Other&&i.Status!=InvoiceStatus.Cancelled&&i.Status!=InvoiceStatus.Draft).Sum(i=>i.TotalAmount);
  var otherPaid=invoices.Where(i=>i.InvoiceType==InvoiceType.Other).Sum(Paid);
  var deposit=invoices.Where(i=>i.InvoiceType==InvoiceType.Deposit).Sum(Paid);
  return (rentDue,rentPaid,otherDue,otherPaid,deposit,deposit+rentPaid+otherPaid-rentDue-otherDue-extras);
 }
 [HttpPost("{id:guid}/settlement")]
 [Authorize(Roles="Landlord")]
 public async Task<IActionResult> Settlement(Guid id,SettlementBody body,CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var row=await db.MoveOutRequests.Include(x=>x.Contract).SingleOrDefaultAsync(x=>x.Id==id&&x.LandlordId==uid,ct);
  if(row is null)return NotFound();
  if(row.Status is not (MoveOutStatus.Approved or MoveOutStatus.InspectionScheduled or MoveOutStatus.AwaitingSettlement)||row.LandlordConfirmedAt!=null||row.TenantConfirmedAt!=null)return Conflict(new{message="Không thể sửa biên bản đã ký. Hãy mở lại quyết toán trước."});
  if(row.Contract.Status!=RentalContractStatus.Active)return Conflict(new{message="Hợp đồng không còn hiệu lực."});
  if(row.RequestedMoveOutDate<row.Contract.StartDate)return Conflict(new{message="Ngày trả nhà không hợp lệ."});
  if(row.RequestedMoveOutDate>Today())return Conflict(new{message="Chưa đến ngày trả nhà đã đăng ký."});
  if(string.IsNullOrWhiteSpace(body.InspectionNotes)||body.InspectionNotes.Length>4000||body.SettlementNotes?.Length>4000||body.ExtraCharges<0||body.ExtraCharges>999999999||body.ElectricReading<0||body.WaterReading<0||body.ElectricReading>999999999||body.WaterReading>999999999||body.ExtraCharges>0&&string.IsNullOrWhiteSpace(body.SettlementNotes))return BadRequest(new{message="Kiểm tra nội dung biên bản và chi phí."});
  var invoices=await db.Invoices.Include(x=>x.Payments).Where(x=>x.ContractId==row.ContractId).ToListAsync(ct);
  if(invoices.SelectMany(x=>x.Payments).Any(x=>x.Status==PaymentStatus.Pending))return Conflict(new{message="Cần xử lý giao dịch đang chờ trước khi quyết toán."});
  row.HandoverAt=RequestedHandoverUtc(row);
  var totals=Calculate(row,invoices,body.ExtraCharges);
  row.DepositPaid=totals.DepositPaid;row.RentDue=totals.RentDue;row.RentPaid=totals.RentPaid;row.OtherDue=totals.OtherDue;row.OtherPaid=totals.OtherPaid;
  row.Outstanding=Math.Max(0,totals.RentDue+totals.OtherDue-totals.RentPaid-totals.OtherPaid);
  row.RefundAmount=Math.Max(0,totals.Balance);row.AdditionalAmount=Math.Max(0,-totals.Balance);
  row.InspectionNotes=body.InspectionNotes.Trim();row.ElectricReading=body.ElectricReading;row.WaterReading=body.WaterReading;row.ExtraCharges=body.ExtraCharges;row.SettlementNotes=body.SettlementNotes;
  row.TenantConfirmedAt=null;row.LandlordConfirmedAt=null;row.TransferReference=null;row.Status=MoveOutStatus.AwaitingSettlement;row.UpdatedAt=DateTime.UtcNow;Notify(row.TenantId,row,"Biên bản và quyết toán chờ xác nhận");await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok();
 }
 [HttpPost("{id:guid}/confirm")]
 public async Task<IActionResult> Confirm(Guid id,ConfirmationBody body,CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  var row=await db.MoveOutRequests.Include(x=>x.Contract).ThenInclude(x=>x.Booking).SingleOrDefaultAsync(x=>x.Id==id&&(x.TenantId==uid||x.LandlordId==uid),ct);
  if(row is null)return NotFound();
  if(row.Status!=MoveOutStatus.AwaitingSettlement||row.Contract.Status!=RentalContractStatus.Active||row.HandoverAt is null)return Conflict(new{message="Chưa thể xác nhận bàn giao."});
  var isOwner=uid==row.LandlordId&&User.IsInRole("Landlord");
  var isRenter=uid==row.TenantId&&User.IsInRole("Tenant");
  if(!isOwner&&!isRenter)return Forbid();
  if(!body.Accepted)return BadRequest(new{message="Bạn cần đồng ý với biên bản bàn giao và xác nhận quyết toán thực tế."});
  if(isOwner&&row.LandlordConfirmedAt!=null||!isOwner&&row.TenantConfirmedAt!=null)return Conflict(new{message="Bạn đã ký xác nhận."});
  var invoices=await db.Invoices.Include(x=>x.Payments).Where(x=>x.ContractId==row.ContractId).ToListAsync(ct);
  var date=SettlementDate(row);
  if(date>Today())return Conflict(new{message="Chưa đến ngày bàn giao."});
  var totals=Calculate(row,invoices,row.ExtraCharges??0);
  if(invoices.SelectMany(i=>i.Payments).Any(p=>p.Status==PaymentStatus.Pending)||row.DepositPaid!=totals.DepositPaid||row.RentDue!=totals.RentDue||row.RentPaid!=totals.RentPaid||row.OtherDue!=totals.OtherDue||row.OtherPaid!=totals.OtherPaid||row.RefundAmount!=Math.Max(0,totals.Balance)||row.AdditionalAmount!=Math.Max(0,-totals.Balance))return Conflict(new{message="Số liệu đã thay đổi. Chủ nhà cần mở lại và lập lại quyết toán."});
  if(isOwner)
  {
   if((row.RefundAmount>0||row.AdditionalAmount>0)&&string.IsNullOrWhiteSpace(body.Reference)||body.Reference?.Length>1000)return BadRequest(new{message="Nhập chứng từ hoặc ghi chú giao dịch thực tế."});
   row.TransferReference=body.Reference?.Trim();row.LandlordConfirmedAt=DateTime.UtcNow;
  }
  else row.TenantConfirmedAt=DateTime.UtcNow;
  if(row.LandlordConfirmedAt!=null&&row.TenantConfirmedAt!=null)
  {
   // Original receipts remain immutable. Outstanding invoices are superseded by the final settlement record.
   foreach(var invoice in invoices.Where(i=>i.Status!=InvoiceStatus.Paid&&i.Status!=InvoiceStatus.Cancelled))invoice.Status=InvoiceStatus.Cancelled;
   if(row.AdditionalAmount>0)
   {
    var amount=row.AdditionalAmount.Value;
    var invoice=new Web2_ThueNha.Models.Payments.Invoice{ContractId=row.ContractId,InvoiceCode="SET-"+row.Id.ToString("N"),InvoiceType=InvoiceType.Other,IssueDate=Today(),DueDate=Today(),TotalAmount=amount,Status=InvoiceStatus.Paid};
    invoice.Items.Add(new Web2_ThueNha.Models.Payments.InvoiceItem{Description="Chênh lệch quyết toán trả nhà đã được hai bên xác nhận",Quantity=1,UnitPrice=amount,Amount=amount});
    invoice.Payments.Add(new Web2_ThueNha.Models.Payments.Payment{PayerId=row.TenantId,Amount=amount,PaymentMethod=PaymentMethod.Other,PaymentProvider="MoveOutSettlement",TransactionCode="SET-"+row.Id.ToString("N"),Status=PaymentStatus.Succeeded,PaidAt=DateTime.UtcNow,ReviewedById=row.LandlordId,ReviewedAt=DateTime.UtcNow});
    db.Invoices.Add(invoice);
   }
   row.CompletedAt=DateTime.UtcNow;row.Status=MoveOutStatus.Completed;
   row.Contract.Status=date<row.Contract.EndDate?RentalContractStatus.Terminated:RentalContractStatus.Completed;
   row.Contract.Booking.UpdatedAt=DateTime.UtcNow;
   Notify(row.LandlordId,row,"Đã hoàn tất bàn giao nhà");Notify(row.TenantId,row,"Đã hoàn tất trả nhà");
  }
  else Notify(isOwner?row.TenantId:row.LandlordId,row,"Bên còn lại đã ký biên bản trả nhà");
  row.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Ok(new{status=row.Status.ToString()});
 }
 [HttpPost("{id:guid}/reopen")]
 [Authorize(Roles="Landlord")]
 public async Task<IActionResult> Reopen(Guid id,CancellationToken ct)
 {
  if(!UserId(out var uid))return Unauthorized();
  var row=await db.MoveOutRequests.SingleOrDefaultAsync(x=>x.Id==id&&x.LandlordId==uid,ct);
  if(row is null)return NotFound();
  if(row.Status!=MoveOutStatus.AwaitingSettlement)return Conflict();
  row.LandlordConfirmedAt=null;row.TenantConfirmedAt=null;row.TransferReference=null;row.UpdatedAt=DateTime.UtcNow;Notify(row.TenantId,row,"Chủ nhà mở lại quyết toán");await db.SaveChangesAsync(ct);return Ok();
 }

}
public sealed record CreateMoveOutRequest(Guid ContractId,DateOnly RequestedMoveOutDate,string Reason,string? TenantNotes);
public sealed record LandlordMoveOutResponse(bool Approve,string? Notes);

public sealed record SettlementBody(string InspectionNotes,decimal? ElectricReading,decimal? WaterReading,decimal ExtraCharges,string? SettlementNotes);
public sealed record ConfirmationBody(string? Reference,bool Accepted);
