using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.Models;

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
                x.Status == PaymentStatus.Succeeded && x.PaidAt.HasValue);

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

        var invoices = db.Invoices.AsNoTracking()
            .Where(x => x.Contract.LandlordId == landlordId && x.Status != InvoiceStatus.Cancelled);
        var overdueCount = await invoices.CountAsync(x =>
            x.DueDate < today && x.Status != InvoiceStatus.Paid, cancellationToken);
        var overdueAmount = await invoices
            .Where(x => x.DueDate < today && x.Status != InvoiceStatus.Paid)
            .Select(x => new { x.TotalAmount, Paid = x.Payments
                .Where(p => p.Status == PaymentStatus.Succeeded)
                .Sum(p => (decimal?)p.Amount) ?? 0m })
            .SumAsync(x => (decimal?)(x.TotalAmount - x.Paid), cancellationToken) ?? 0m;
        var outstandingAmount = await invoices
            .Where(x => x.Status != InvoiceStatus.Paid)
            .Select(x => new { x.TotalAmount, Paid = x.Payments
                .Where(p => p.Status == PaymentStatus.Succeeded)
                .Sum(p => (decimal?)p.Amount) ?? 0m })
            .SumAsync(x => (decimal?)(x.TotalAmount - x.Paid), cancellationToken) ?? 0m;

        var trendRows = await payments
            .Where(x => x.PaidAt >= rangeStart.ToDateTime(TimeOnly.MinValue).AddHours(-7) &&
                x.Invoice.InvoiceType != InvoiceType.Deposit)
            .GroupBy(x => new { x.PaidAt!.Value.Year, x.PaidAt.Value.Month })
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

        return Ok(new
        {
            months,
            receivedAllTime,
            receivedThisMonth,
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
            trend
        });
    }
}
