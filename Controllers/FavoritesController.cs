using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Favorites;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public sealed class FavoritesController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 50;

    /// <summary>Danh sách nhà yêu thích hiện còn được phép hiển thị công khai.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedFavoritesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavorites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize)
            return BadRequest(new { message = "page phải từ 1; pageSize phải từ 1 đến 50." });

        IQueryable<Favorite> query = db.Favorites
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Property.ModerationStatus == PropertyModerationStatus.Approved &&
                x.Property.ListingStatus == PropertyListingStatus.Published);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = page > totalPages
            ? new List<FavoritePropertyItem>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.PropertyId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new FavoritePropertyItem(
                    x.PropertyId,
                    x.Property.Title,
                    x.Property.PropertyType.Name,
                    x.Property.PricePerMonth,
                    x.Property.Area,
                    x.Property.Bedrooms,
                    x.Property.Bathrooms,
                    x.Property.AddressLine,
                    x.Property.WardName,
                    x.Property.ProvinceName,
                    x.Property.Images
                        .OrderByDescending(image => image.IsPrimary)
                        .ThenBy(image => image.DisplayOrder)
                        .ThenBy(image => image.Id)
                        .Select(image => image.ImageUrl)
                        .FirstOrDefault(),
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(new PagedFavoritesResponse(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

    /// <summary>Thêm nhà đang công khai vào danh sách yêu thích; request lặp an toàn.</summary>
    [HttpPost("{propertyId:guid}")]
    [ProducesResponseType(typeof(FavoriteStatusResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(FavoriteStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddFavorite(Guid propertyId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var isPublic = await db.Properties.AnyAsync(
            x => x.Id == propertyId &&
                 x.ModerationStatus == PropertyModerationStatus.Approved &&
                 x.ListingStatus == PropertyListingStatus.Published,
            cancellationToken);

        if (!isPublic)
            return NotFound(new { message = "Không tìm thấy tin đăng đang được công khai." });

        var alreadyExists = await db.Favorites.AnyAsync(
            x => x.UserId == userId && x.PropertyId == propertyId,
            cancellationToken);

        if (alreadyExists)
            return Ok(new FavoriteStatusResponse(propertyId, true));

        var favorite = new Favorite
        {
            UserId = userId,
            PropertyId = propertyId,
            CreatedAt = DateTime.UtcNow
        };

        db.Favorites.Add(favorite);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.GetBaseException() is SqlException sqlException &&
            sqlException.Number is 2601 or 2627)
        {
            // Khóa chính kép (UserId, PropertyId) xử lý hai request thêm đồng thời.
            db.Entry(favorite).State = EntityState.Detached;

            if (await db.Favorites.AnyAsync(
                    x => x.UserId == userId && x.PropertyId == propertyId,
                    cancellationToken))
                return Ok(new FavoriteStatusResponse(propertyId, true));

            throw;
        }

        return CreatedAtAction(
            nameof(GetFavoriteStatus),
            new { propertyId },
            new FavoriteStatusResponse(propertyId, true));
    }

    /// <summary>Bỏ yêu thích; xóa lặp nhiều lần vẫn an toàn.</summary>
    [HttpDelete("{propertyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveFavorite(Guid propertyId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await db.Favorites
            .Where(x => x.UserId == userId && x.PropertyId == propertyId)
            .ExecuteDeleteAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Kiểm tra trạng thái đã lưu của một tin, kể cả khi tin tạm thời bị ẩn.</summary>
    [HttpGet("{propertyId:guid}/status")]
    [ProducesResponseType(typeof(FavoriteStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavoriteStatus(Guid propertyId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, cancellationToken))
            return NotFound(new { message = "Không tìm thấy tin đăng." });

        var isFavorite = await db.Favorites.AnyAsync(
            x => x.UserId == userId && x.PropertyId == propertyId,
            cancellationToken);

        return Ok(new FavoriteStatusResponse(propertyId, isFavorite));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
