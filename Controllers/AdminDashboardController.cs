using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public sealed class AdminDashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
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
            x => x.Status == InvoiceStatus.Overdue, cancellationToken);
        var pendingPayments = await db.Payments.CountAsync(
            x => x.Status == PaymentStatus.Pending, cancellationToken);
        var successfulPayments = await db.Payments.CountAsync(
            x => x.Status == PaymentStatus.Succeeded, cancellationToken);
        var successfulPaymentAmount = await db.Payments
            .Where(x => x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var totalReviews = await db.Reviews.CountAsync(cancellationToken);
        var totalModerationActions = await db.PropertyModerationLogs.CountAsync(cancellationToken);

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
            totalModerationActions
        });
    }
}
