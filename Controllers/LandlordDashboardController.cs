using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/landlord/dashboard")]
[Authorize(Roles = "Landlord")]
public sealed class LandlordDashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("financial-summary")]
    public async Task<IActionResult> GetFinancialSummary(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        if (months is < 3 or > 24)
            return BadRequest(new { message = "Khoảng thống kê phải từ 3 đến 24 tháng." });

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var landlordId))
            return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var rangeStart = monthStart.AddMonths(-(months - 1));
        var payments = db.Payments.AsNoTracking()
            .Where(x => x.Invoice.Contract.LandlordId == landlordId &&
                x.Status == PaymentStatus.Succeeded && x.PaidAt.HasValue && x.PaidAt <= DateTime.UtcNow);

        var receivedAllTime = await payments.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var receivedThisMonth = await payments
            .Where(x => x.PaidAt >= monthStart.ToDateTime(TimeOnly.MinValue).AddHours(-7))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var rentReceived = await payments
            .Where(x => x.Invoice.InvoiceType == InvoiceType.MonthlyRent)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var depositReceived = await payments
            .Where(x => x.Invoice.InvoiceType == InvoiceType.Deposit)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var otherReceived = await payments
            .Where(x => x.Invoice.InvoiceType == InvoiceType.Other)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var refunds = db.MoveOutRequests.AsNoTracking().Where(x=>x.LandlordId==landlordId && x.Status==Web2_ThueNha.Models.Contracts.MoveOutStatus.Completed);
        var refundedAllTime=await refunds.SumAsync(x=>x.RefundAmount,cancellationToken)??0m;
        var refundedThisMonth=await refunds.Where(x=>x.CompletedAt>=monthStart.ToDateTime(TimeOnly.MinValue).AddHours(-7)).SumAsync(x=>x.RefundAmount,cancellationToken)??0m;
        var invoices = db.Invoices.AsNoTracking()
            .Where(x => x.Contract.LandlordId == landlordId && x.Status != InvoiceStatus.Cancelled && x.Status != InvoiceStatus.Draft);
        // Materialize per-invoice balances to avoid SQL Server SUM over correlated aggregate.
        var openBalances = await invoices.Where(x=>x.Status!=InvoiceStatus.Paid)
            .Select(x=>new {x.DueDate,x.TotalAmount,Paid=x.Payments.Where(p=>p.Status==PaymentStatus.Succeeded).Sum(p=>(decimal?)p.Amount)??0m})
            .ToListAsync(cancellationToken);
        var balances=openBalances.Select(x=>new {x.DueDate,Amount=Math.Max(0m,x.TotalAmount-x.Paid)}).Where(x=>x.Amount>0).ToList();
        var overdueCount=balances.Count(x=>x.DueDate<today);
        var overdueAmount=balances.Where(x=>x.DueDate<today).Sum(x=>x.Amount);
        var outstandingAmount=balances.Sum(x=>x.Amount);
        var dueSoonAmount=balances.Where(x=>x.DueDate>=today&&x.DueDate<=today.AddDays(7)).Sum(x=>x.Amount);
        var trendRows = await payments
            .Where(x => x.PaidAt >= rangeStart.ToDateTime(TimeOnly.MinValue).AddHours(-7) &&
                x.Invoice.InvoiceType != InvoiceType.Deposit)
            .GroupBy(x => new { x.PaidAt!.Value.AddHours(7).Year, x.PaidAt.Value.AddHours(7).Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Amount = g.Sum(x => x.Amount),
                Rent = g.Where(x => x.Invoice.InvoiceType == InvoiceType.MonthlyRent).Sum(x => x.Amount),
                Other = g.Where(x => x.Invoice.InvoiceType == InvoiceType.Other).Sum(x => x.Amount)
            })
            .ToListAsync(cancellationToken);

        var trend = Enumerable.Range(0, months).Select(offset =>
        {
            var month = rangeStart.AddMonths(offset);
            var row = trendRows.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
            return new
            {
                year = month.Year,
                month = month.Month,
                label = $"{month.Month:00}/{month.Year}",
                received = row?.Amount ?? 0m,
                rent = row?.Rent ?? 0m,
                other = row?.Other ?? 0m
            };
        }).ToArray();

        var propertyCount = await db.Properties.CountAsync(x => x.LandlordId == landlordId, cancellationToken);
        var activeContracts = await db.RentalContracts.CountAsync(x =>
            x.LandlordId == landlordId && x.Status == RentalContractStatus.Active, cancellationToken);
        var endingSoon = await db.RentalContracts.CountAsync(x =>
            x.LandlordId == landlordId && x.Status == RentalContractStatus.Active &&
            x.EndDate >= today && x.EndDate <= today.AddDays(30), cancellationToken);
        var pendingPayments = await db.Payments.CountAsync(x =>
            x.Invoice.Contract.LandlordId == landlordId && x.Status == PaymentStatus.Pending, cancellationToken);
        var pendingRequests = await db.Bookings.CountAsync(x =>
            x.Property.LandlordId == landlordId && x.Status == BookingStatus.Pending, cancellationToken);

        var openMoveOuts=db.MoveOutRequests.AsNoTracking().Where(x=>x.LandlordId==landlordId&&x.Status!=MoveOutStatus.Completed&&x.Status!=MoveOutStatus.Cancelled&&x.Status!=MoveOutStatus.Rejected);
        var pendingMoveOuts=await openMoveOuts.CountAsync(x=>x.Status==MoveOutStatus.Pending,cancellationToken);
        var handoverDue=await openMoveOuts.CountAsync(x=>(x.Status==MoveOutStatus.Approved||x.Status==MoveOutStatus.InspectionScheduled)&&x.RequestedMoveOutDate<=today,cancellationToken);
        var waitingMoveOutSignature=await openMoveOuts.CountAsync(x=>x.Status==MoveOutStatus.AwaitingSettlement&&x.LandlordConfirmedAt==null,cancellationToken);
        var openMoveOutCount=await openMoveOuts.CountAsync(cancellationToken);
        var upcomingHandovers=await openMoveOuts.CountAsync(x=>(x.Status==MoveOutStatus.Approved||x.Status==MoveOutStatus.InspectionScheduled)&&x.RequestedMoveOutDate>today,cancellationToken);
        var waitingTenantSignature=await openMoveOuts.CountAsync(x=>x.Status==MoveOutStatus.AwaitingSettlement&&x.LandlordConfirmedAt!=null&&x.TenantConfirmedAt==null,cancellationToken);
        var moveOutItems=await openMoveOuts.OrderByDescending(x=>x.Status==MoveOutStatus.Pending).ThenBy(x=>x.RequestedMoveOutDate).ThenByDescending(x=>x.CreatedAt).Take(5)
            .Select(x=>new {x.Id,PropertyTitle=x.Contract.Property.Title,x.Contract.ContractCode,x.RequestedMoveOutDate,Status=x.Status.ToString(),TenantName=x.Tenant.FullName,x.LandlordConfirmedAt,x.TenantConfirmedAt}).ToListAsync(cancellationToken);
        var rentedPropertyCount=await db.RentalContracts.Where(x=>x.LandlordId==landlordId&&x.Status==RentalContractStatus.Active&&x.StartDate<=today&&(x.EndDate>today||db.MoveOutRequests.Any(m=>m.ContractId==x.Id&&m.Status!=MoveOutStatus.Completed&&m.Status!=MoveOutStatus.Cancelled&&m.Status!=MoveOutStatus.Rejected))).Select(x=>x.PropertyId).Distinct().CountAsync(cancellationToken);
        var signingCount=await db.RentalContracts.CountAsync(x=>x.LandlordId==landlordId&&x.Status==RentalContractStatus.AwaitingSignature,cancellationToken);
        var overdueItems=await invoices.Where(x=>x.Status!=InvoiceStatus.Paid&&x.DueDate<today).OrderBy(x=>x.DueDate).ThenBy(x=>x.Id).Take(5)
            .Select(x=>new {x.Id,x.InvoiceCode,PropertyTitle=x.Contract.Property.Title,x.DueDate,x.TotalAmount,Paid=x.Payments.Where(p=>p.Status==PaymentStatus.Succeeded).Sum(p=>(decimal?)p.Amount)??0m}).ToListAsync(cancellationToken);
        return Ok(new
        {
            months,
            receivedAllTime,
            receivedThisMonth,
            refundedAllTime,
            refundedThisMonth,
            netReceivedAllTime=receivedAllTime-refundedAllTime,
            netReceivedThisMonth=receivedThisMonth-refundedThisMonth,
            rentReceived,
            depositReceived,
            otherReceived,
            outstandingAmount,
            overdueAmount,
            overdueCount,
            pendingPayments,
            pendingRequests,
            propertyCount,
            activeContracts,
            endingSoon,
            rentedPropertyCount, signingCount, dueSoonAmount, pendingMoveOuts, handoverDue, waitingMoveOutSignature, waitingTenantSignature, upcomingHandovers, openMoveOutCount, moveOutItems, overdueItems,
            trend
        });
    }
}
