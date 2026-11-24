using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNHa.DTOs.Properties;

namespace Web2_ThueNHa.Controllers;

[ApiController]
[Route("api/properties")]
public sealed class PropertiesController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 50;

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedPropertyResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProperties(
        [FromQuery] string? keyword,
        [FromQuery] string? province,
        [FromQuery] int? propertyTypeId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minArea,
        [FromQuery] decimal? maxArea,
        [FromQuery] int? minBedrooms,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            minPrice < 0 || maxPrice < 0 || minArea < 0 || maxArea < 0 ||
            minBedrooms < 0 || (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice) ||
            (minArea.HasValue && maxArea.HasValue && minArea > maxArea))
            return BadRequest(new { message = "Tham số lọc hoặc phân trang không hợp lệ." });

        var query = db.Properties.AsNoTracking()
            .Where(x => x.ListingStatus == PropertyListingStatus.Published &&
                        x.ModerationStatus == PropertyModerationStatus.Approved);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.Title.Contains(term) ||
                                     x.AddressLine.Contains(term) ||
                                     x.ProvinceName.Contains(term) ||
                                     (x.WardName != null && x.WardName.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            var provinceName = province.Trim();
            query = query.Where(x => x.ProvinceName.Contains(provinceName));
        }

        if (propertyTypeId.HasValue)
            query = query.Where(x => x.PropertyTypeId == propertyTypeId.Value);
        if (minPrice.HasValue)
            query = query.Where(x => x.PricePerMonth >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(x => x.PricePerMonth <= maxPrice.Value);
        if (minArea.HasValue)
            query = query.Where(x => x.Area >= minArea.Value);
        if (maxArea.HasValue)
            query = query.Where(x => x.Area <= maxArea.Value);
        if (minBedrooms.HasValue)
            query = query.Where(x => x.Bedrooms >= minBedrooms.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.PropertyType)
            .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
            .ToListAsync(cancellationToken);

        return Ok(new PagedPropertyResponse(
            items.Select(ToResponse).ToArray(),
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProperty(Guid id, CancellationToken cancellationToken)
    {
        var property = await db.Properties.AsNoTracking()
            .Include(x => x.PropertyType)
            .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        var isOwner = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                      && userId == property.LandlordId;
        var isAdmin = User.IsInRole("Admin");
        if (!isOwner && !isAdmin &&
            (property.ListingStatus != PropertyListingStatus.Published ||
             property.ModerationStatus != PropertyModerationStatus.Approved))
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        return Ok(ToResponse(property));
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> GetMyProperties(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var properties = await db.Properties.AsNoTracking()
            .Where(x => x.LandlordId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Include(x => x.PropertyType)
            .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
            .ToListAsync(cancellationToken);

        return Ok(properties.Select(ToResponse));
    }

    [HttpPost]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(PropertyResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProperty(
        PropertyCreateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (!await db.PropertyTypes.AnyAsync(x => x.Id == request.PropertyTypeId, cancellationToken))
            return BadRequest(new { message = "Loại nhà không tồn tại." });

        var property = new Property
        {
            LandlordId = userId,
            PropertyTypeId = request.PropertyTypeId,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            PricePerMonth = request.PricePerMonth,
            Area = request.Area,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            AddressLine = request.AddressLine.Trim(),
            WardName = Clean(request.WardName),
            ProvinceName = request.ProvinceName.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ModerationStatus = PropertyModerationStatus.Pending,
            ListingStatus = PropertyListingStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(property).Reference(x => x.PropertyType).LoadAsync(cancellationToken);

        return CreatedAtAction(nameof(GetProperty), new { id = property.Id }, ToResponse(property));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> UpdateProperty(
        Guid id, PropertyUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });
        if (property.LandlordId != userId)
            return Forbid();
        if (!await db.PropertyTypes.AnyAsync(x => x.Id == request.PropertyTypeId, cancellationToken))
            return BadRequest(new { message = "Loại nhà không tồn tại." });

        property.PropertyTypeId = request.PropertyTypeId;
        property.Title = request.Title.Trim();
        property.Description = Clean(request.Description);
        property.PricePerMonth = request.PricePerMonth;
        property.Area = request.Area;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.AddressLine = request.AddressLine.Trim();
        property.WardName = Clean(request.WardName);
        property.ProvinceName = request.ProvinceName.Trim();
        property.Latitude = request.Latitude;
        property.Longitude = request.Longitude;
        property.ModerationStatus = PropertyModerationStatus.Pending;
        property.ListingStatus = PropertyListingStatus.Draft;
        property.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(property).Reference(x => x.PropertyType).LoadAsync(cancellationToken);
        var images = await db.PropertyImages.AsNoTracking()
            .Where(x => x.PropertyId == id).OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
        property.Images = images;
        return Ok(ToResponse(property));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, PropertyStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (!Enum.IsDefined(request.Status) || request.Status == PropertyListingStatus.Draft)
            return BadRequest(new { message = "Trạng thái yêu cầu không hợp lệ." });

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });
        if (property.LandlordId != userId)
            return Forbid();
        if (request.Status == PropertyListingStatus.Published &&
            property.ModerationStatus != PropertyModerationStatus.Approved)
            return Conflict(new { message = "Tin đăng cần được duyệt trước khi hiển thị." });

        property.ListingStatus = request.Status;
        property.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> AddImage(
        Guid id, PropertyImageCreateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var imageUri) ||
            imageUri.Scheme != Uri.UriSchemeHttps)
            return BadRequest(new { message = "Đường dẫn hình ảnh phải là URL HTTPS hợp lệ." });

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });
        if (property.LandlordId != userId)
            return Forbid();

        var imageCount = await db.PropertyImages.CountAsync(x => x.PropertyId == id, cancellationToken);
        if (imageCount >= 20)
            return Conflict(new { message = "Mỗi tin đăng được tối đa 20 hình ảnh." });

        var image = new PropertyImage
        {
            PropertyId = id,
            ImageUrl = request.ImageUrl.Trim(),
            IsPrimary = imageCount == 0,
            DisplayOrder = imageCount
        };
        db.PropertyImages.Add(image);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/properties/{id}", new PropertyImageResponse(
            image.Id, image.ImageUrl, image.IsPrimary, image.DisplayOrder));
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> DeleteImage(
        Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });
        if (property.LandlordId != userId)
            return Forbid();

        var image = await db.PropertyImages.SingleOrDefaultAsync(
            x => x.Id == imageId && x.PropertyId == id, cancellationToken);
        if (image is null)
            return NotFound(new { message = "Không tìm thấy hình ảnh." });

        var wasPrimary = image.IsPrimary;
        db.PropertyImages.Remove(image);
        if (wasPrimary)
        {
            var nextImage = await db.PropertyImages.Where(x => x.PropertyId == id && x.Id != imageId)
                .OrderBy(x => x.DisplayOrder).FirstOrDefaultAsync(cancellationToken);
            if (nextImage is not null)
                nextImage.IsPrimary = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/images/{imageId:guid}/primary")]
    [Authorize(Roles = "Landlord")]
    public async Task<IActionResult> SetPrimaryImage(
        Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var property = await db.Properties.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });
        if (property.LandlordId != userId)
            return Forbid();

        var image = await db.PropertyImages.SingleOrDefaultAsync(
            x => x.Id == imageId && x.PropertyId == id, cancellationToken);
        if (image is null)
            return NotFound(new { message = "Không tìm thấy hình ảnh." });

        var images = await db.PropertyImages.Where(x => x.PropertyId == id)
            .ToListAsync(cancellationToken);
        foreach (var item in images)
            item.IsPrimary = item.Id == imageId;

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PropertyResponse ToResponse(Property property) =>
        new(
            property.Id,
            property.LandlordId,
            property.PropertyTypeId,
            property.PropertyType?.Name ?? string.Empty,
            property.Title,
            property.Description,
            property.PricePerMonth,
            property.Area,
            property.Bedrooms,
            property.Bathrooms,
            property.AddressLine,
            property.WardName,
            property.ProvinceName,
            property.Latitude,
            property.Longitude,
            property.ModerationStatus.ToString(),
            property.ListingStatus.ToString(),
            property.CreatedAt,
            property.UpdatedAt,
            property.Images.OrderBy(x => x.DisplayOrder)
                .Select(x => new PropertyImageResponse(x.Id, x.ImageUrl, x.IsPrimary, x.DisplayOrder))
                .ToArray());
}
