using System.Security.Claims;
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
  var rows=await q.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Select(x=>new {x.Id,x.ContractId,x.Contract.ContractCode,PropertyTitle=x.Contract.Property.Title,x.RequestedMoveOutDate,x.Reason,x.TenantNotes,x.LandlordNotes,Status=x.Status.ToString(),x.CreatedAt,x.CompletedAt,TenantName=x.Tenant.FullName,LandlordName=x.Landlord.FullName}).ToListAsync(ct);
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
  var contract=await db.RentalContracts.Include(x=>x.Property).SingleOrDefaultAsync(x=>x.Id==body.ContractId&&x.TenantId==uid,ct);
  if(contract is null)return NotFound(new{message="Không tìm thấy hợp đồng."});
  if(contract.Status!=RentalContractStatus.Active)return Conflict(new{message="Chỉ hợp đồng đang hiệu lực mới được gửi yêu cầu trả nhà."});
  var exists=await db.MoveOutRequests.AnyAsync(x=>x.ContractId==contract.Id&&x.Status!=MoveOutStatus.Rejected&&x.Status!=MoveOutStatus.Cancelled&&x.Status!=MoveOutStatus.Completed,ct);
  if(exists)return Conflict(new{message="Hợp đồng đã có yêu cầu trả nhà đang xử lý."});
  var row=new MoveOutRequest{ContractId=contract.Id,TenantId=uid,LandlordId=contract.LandlordId,RequestedMoveOutDate=body.RequestedMoveOutDate,Reason=body.Reason.Trim(),TenantNotes=body.TenantNotes?.Trim()};
  db.MoveOutRequests.Add(row);await db.SaveChangesAsync(ct);
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
  if(body.Approve){row.Status=MoveOutStatus.Approved;}else{row.Status=MoveOutStatus.Rejected;}
  row.LandlordNotes=body.Notes?.Trim();row.UpdatedAt=DateTime.UtcNow;await db.SaveChangesAsync(ct);return Ok(new{status=row.Status.ToString()});
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
}
public sealed record CreateMoveOutRequest(Guid ContractId,DateOnly RequestedMoveOutDate,string Reason,string? TenantNotes);
public sealed record LandlordMoveOutResponse(bool Approve,string? Notes);
