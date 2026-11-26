using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models.Contracts;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public sealed class AdminDashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var now=DateTime.UtcNow;
        var today=DateOnly.FromDateTime(now.AddHours(7));
        var monthStart=new DateOnly(today.Year,today.Month,1);
        var rangeStart=monthStart.AddMonths(-11).ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var monthUtc=monthStart.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        var totalUsers = await db.Users.CountAsync(cancellationToken);
        var activeUsers = await db.Users.CountAsync(x => x.IsActive, cancellationToken);
        var totalProperties = await db.Properties.CountAsync(cancellationToken);
        var pendingProperties = await db.Properties.CountAsync(
            x => x.ModerationStatus == PropertyModerationStatus.Pending, cancellationToken);
        var publishedProperties = await db.Properties.CountAsync(
            x => x.ModerationStatus == PropertyModerationStatus.Approved &&
                 x.ListingStatus == PropertyListingStatus.Published, cancellationToken);
        var pendingBookings = await db.Bookings.CountAsync(
            x => x.Status == BookingStatus.Pending, cancellationToken);
        var activeContracts = await db.RentalContracts.CountAsync(
            x => x.Status == RentalContractStatus.Active, cancellationToken);
        var totalBookings = await db.Bookings.CountAsync(cancellationToken);
        var totalContracts = await db.RentalContracts.CountAsync(cancellationToken);
        var totalInvoices = await db.Invoices.CountAsync(cancellationToken);
        var overdueInvoices = await db.Invoices.CountAsync(
            x => x.DueDate<today && x.Status!=InvoiceStatus.Draft && x.Status!=InvoiceStatus.Cancelled && x.Status!=InvoiceStatus.Paid, cancellationToken);
        var pendingPayments = await db.Payments.CountAsync(
            x => x.Status == PaymentStatus.Pending, cancellationToken);
        var successfulPayments = await db.Payments.CountAsync(
            x => x.Status == PaymentStatus.Succeeded, cancellationToken);
        var successfulPaymentAmount = await db.Payments
            .Where(x => x.Status == PaymentStatus.Succeeded && x.PaidAt<=now)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var totalReviews = await db.Reviews.CountAsync(cancellationToken);
        var totalModerationActions = await db.PropertyModerationLogs.CountAsync(cancellationToken);

        var roleCounts=await db.Users.SelectMany(u=>u.UserRoles).GroupBy(x=>x.Role.Name).Select(g=>new {role=g.Key,count=g.Count()}).ToListAsync(cancellationToken);
        var newUsersThisMonth=await db.Users.CountAsync(x=>x.CreatedAt>=monthUtc&&x.CreatedAt<=now,cancellationToken);
        var awaitingSignature=await db.RentalContracts.CountAsync(x=>x.Status==RentalContractStatus.AwaitingSignature,cancellationToken);
        var endingSoon=await db.RentalContracts.CountAsync(x=>x.Status==RentalContractStatus.Active&&x.EndDate>=today&&x.EndDate<=today.AddDays(30),cancellationToken);
        var openMoveOuts=db.MoveOutRequests.AsNoTracking().Where(x=>x.Status!=MoveOutStatus.Completed&&x.Status!=MoveOutStatus.Rejected&&x.Status!=MoveOutStatus.Cancelled);
        var pendingMoveOuts=await openMoveOuts.CountAsync(x=>x.Status==MoveOutStatus.Pending,cancellationToken);
        var openMoveOutCount=await openMoveOuts.CountAsync(cancellationToken);
        var handoverDue=await openMoveOuts.CountAsync(x=>(x.Status==MoveOutStatus.Approved||x.Status==MoveOutStatus.InspectionScheduled)&&x.RequestedMoveOutDate<=today,cancellationToken);
        var awaitingSettlement=await openMoveOuts.CountAsync(x=>x.Status==MoveOutStatus.AwaitingSettlement,cancellationToken);
        var completedMoveOuts=await db.MoveOutRequests.CountAsync(x=>x.Status==MoveOutStatus.Completed,cancellationToken);
        var receivedThisMonth=await db.Payments.Where(x=>x.Status==PaymentStatus.Succeeded&&x.PaidAt>=monthUtc&&x.PaidAt<=now).SumAsync(x=>(decimal?)x.Amount,cancellationToken)??0m;
        var refundedThisMonth=await db.MoveOutRequests.Where(x=>x.Status==MoveOutStatus.Completed&&x.CompletedAt>=monthUtc&&x.CompletedAt<=now).SumAsync(x=>x.RefundAmount,cancellationToken)??0m;
        var balances=await db.Invoices.Where(x=>x.Status!=InvoiceStatus.Paid&&x.Status!=InvoiceStatus.Cancelled&&x.Status!=InvoiceStatus.Draft).Select(x=>new{x.DueDate,x.TotalAmount,Paid=x.Payments.Where(p=>p.Status==PaymentStatus.Succeeded).Sum(p=>(decimal?)p.Amount)??0m}).ToListAsync(cancellationToken);
        var outstandingAmount=balances.Sum(x=>Math.Max(0m,x.TotalAmount-x.Paid));
        var overdueAmount=balances.Where(x=>x.DueDate<today).Sum(x=>Math.Max(0m,x.TotalAmount-x.Paid));
        var monthlyPayments=await db.Payments.Where(x=>x.Status==PaymentStatus.Succeeded&&x.PaidAt>=rangeStart&&x.PaidAt<=now).GroupBy(x=>new{x.PaidAt!.Value.AddHours(7).Year,x.PaidAt.Value.AddHours(7).Month}).Select(g=>new{g.Key.Year,g.Key.Month,amount=g.Sum(x=>x.Amount)}).ToListAsync(cancellationToken);
        var monthlyRefunds=await db.MoveOutRequests.Where(x=>x.Status==MoveOutStatus.Completed&&x.CompletedAt>=rangeStart&&x.CompletedAt<=now).GroupBy(x=>new{x.CompletedAt!.Value.AddHours(7).Year,x.CompletedAt.Value.AddHours(7).Month}).Select(g=>new{g.Key.Year,g.Key.Month,amount=g.Sum(x=>x.RefundAmount)??0m}).ToListAsync(cancellationToken);
        var trend=Enumerable.Range(0,12).Select(i=>{var m=monthStart.AddMonths(i-11);return new{label=$"{m.Month:00}/{m.Year}",received=monthlyPayments.FirstOrDefault(x=>x.Year==m.Year&&x.Month==m.Month)?.amount??0m,refunded=monthlyRefunds.FirstOrDefault(x=>x.Year==m.Year&&x.Month==m.Month)?.amount??0m};}).ToArray();
        var moderationQueue=await db.Properties.Where(x=>x.ModerationStatus==PropertyModerationStatus.Pending).OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).Take(5).Select(x=>new{x.Id,x.Title,landlordName=x.Landlord.FullName,x.CreatedAt}).ToListAsync(cancellationToken);
        return Ok(new
        {
            totalUsers,
            activeUsers,
            totalProperties,
            pendingProperties,
            publishedProperties,
            totalBookings,
            pendingBookings,
            totalContracts,
            activeContracts,
            totalInvoices,
            overdueInvoices,
            pendingPayments,
            successfulPayments,
            successfulPaymentAmount,
            totalReviews,
            totalModerationActions,
            roleCounts,newUsersThisMonth,awaitingSignature,endingSoon,pendingMoveOuts,openMoveOutCount,handoverDue,awaitingSettlement,completedMoveOuts,receivedThisMonth,refundedThisMonth,outstandingAmount,overdueAmount,trend,moderationQueue
        });
    }
}
