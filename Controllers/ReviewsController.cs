using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Reviews;
using Web2_ThueNha.Models;
using Web2_ThueNha.Models.Contracts;
using Web2_ThueNha.Models.Interactions;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController(AppDbContext db) : ControllerBase
{
    /// <summary>Đánh giá công khai của một căn nhà; chỉ trả về nội dung đánh giá, không lộ thông tin cá nhân.</summary>
    [HttpGet("properties/{propertyId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPropertyReviews(
        Guid propertyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 30 || page > int.MaxValue / pageSize)
            return BadRequest(new { message = "Tham số phân trang đánh giá không hợp lệ." });

        var propertyExists = await db.Properties.AnyAsync(x => x.Id == propertyId, cancellationToken);
        if (!propertyExists)
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        var query = db.Reviews.AsNoTracking()
            .Where(x => x.Contract.PropertyId == propertyId);

        var total = await query.CountAsync(cancellationToken);
        var average = await query.Select(x => (double?)x.Rating)
            .AverageAsync(cancellationToken) ?? 0d;

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PropertyReviewItem(
                x.Id, x.Rating, x.Comment, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PropertyReviewsResponse(average, total, items));
    }

    /// <summary>Danh sách đánh giá do người thuê hiện tại đã gửi.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> GetMyReviews(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var items = await db.Reviews.AsNoTracking()
            .Where(x => x.Contract.TenantId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MyReviewItem(
                x.Id,
                x.ContractId,
                x.Contract.PropertyId,
                x.Contract.Property.Title,
                x.Rating,
                x.Comment,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Mỗi hợp đồng chỉ được người thuê đánh giá một lần sau khi thời hạn thuê kết thúc.</summary>
    [HttpPost("contracts/{contractId:guid}")]
    [Authorize(Roles = "Tenant")]
    public async Task<IActionResult> CreateReview(
        Guid contractId,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var contract = await db.RentalContracts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract is null)
            return NotFound(new { message = "Không tìm thấy hợp đồng." });

        if (contract.TenantId != userId)
            return Forbid();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasEnded = contract.Status == RentalContractStatus.Completed ||
                       (contract.Status == RentalContractStatus.Active && contract.EndDate <= today);
        if (!hasEnded)
            return Conflict(new { message = "Bạn chỉ có thể đánh giá sau khi thời hạn thuê kết thúc." });

        var alreadyReviewed = await db.Reviews.AnyAsync(
            x => x.ContractId == contractId, cancellationToken);
        if (alreadyReviewed)
            return Conflict(new { message = "Hợp đồng này đã được đánh giá." });

        var review = new Review
        {
            ContractId = contractId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment)
                ? null
                : request.Comment.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetMyReviews),
            new { id = review.Id },
            new
            {
                review.Id,
                review.ContractId,
                contract.PropertyId,
                review.Rating,
                review.Comment,
                review.CreatedAt
            });
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
