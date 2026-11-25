using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Properties;
using Web2_ThueNha.Repositories.Properties;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/properties")]
public sealed class PropertiesController(AppDbContext db, IPropertyQueryRepository propertyQueries) : ControllerBase
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
            page > int.MaxValue / pageSize ||
            (propertyTypeId.HasValue && propertyTypeId.Value < 1) ||
            minPrice < 0 || maxPrice < 0 || minArea < 0 || maxArea < 0 ||
            minBedrooms < 0 || (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice) ||
            (minArea.HasValue && maxArea.HasValue && minArea > maxArea))
            return BadRequest(new { message = "Tham số lọc hoặc phân trang không hợp lệ." });

        var query = propertyQueries.GetPublicPropertiesQuery();

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
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = page > totalPages
            ? new List<Property>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(x => x.PropertyType)
                .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
                .Include(x => x.PropertyAmenities).ThenInclude(x => x.Amenity)
                .ToListAsync(cancellationToken);

        return Ok(new PagedPropertyResponse(
            await Task.WhenAll(items.Select(x => ToResponseAsync(x, cancellationToken))),
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProperty(Guid id, CancellationToken cancellationToken)
    {
        var property = await propertyQueries.GetDetailAsync(id, cancellationToken);

        if (property is null)
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        var isOwner = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                      && userId == property.LandlordId;
        var isAdmin = User.IsInRole("Admin");
        if (!isOwner && !isAdmin &&
            (property.ListingStatus != PropertyListingStatus.Published ||
             property.ModerationStatus != PropertyModerationStatus.Approved))
            return NotFound(new { message = "Không tìm thấy căn nhà." });

        return Ok(await ToResponseAsync(property, cancellationToken));
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(PagedMyPropertiesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyProperties(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] PropertyListingStatus? status = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)) ||
            (keyword is { Length: > 100 }))
            return BadRequest(new { message = "Tham số tìm kiếm, phân trang hoặc trạng thái không hợp lệ." });

        // Luôn giới hạn dữ liệu theo chủ nhà trong JWT; không nhận LandlordId từ client.
        IQueryable<Property> query = propertyQueries.GetLandlordPropertiesQuery(userId);

        if (status.HasValue)
            query = query.Where(x => x.ListingStatus == status.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.Title.Contains(term) ||
                                     x.AddressLine.Contains(term) ||
                                     x.ProvinceName.Contains(term) ||
                                     (x.WardName != null && x.WardName.Contains(term)));
        }

        // Đếm sau khi áp dụng toàn bộ điều kiện lọc, trước khi phân trang.
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var properties = page > totalPages
            ? new List<Property>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(x => x.PropertyType)
                .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
                .ToListAsync(cancellationToken);

        return Ok(new PagedMyPropertiesResponse(
            await Task.WhenAll(properties.Select(x => ToResponseAsync(x, cancellationToken))),
            page,
            pageSize,
            totalCount,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
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
            DepositAmount = request.DepositAmount,
            MaxOccupants = request.MaxOccupants,
            FurnishingDescription = Clean(request.FurnishingDescription),
            AdditionalFeesDescription = Clean(request.AdditionalFeesDescription),
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

        return CreatedAtAction(nameof(GetProperty), new { id = property.Id }, await ToResponseAsync(property, cancellationToken));
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
        property.DepositAmount = request.DepositAmount;
        property.MaxOccupants = request.MaxOccupants;
        property.FurnishingDescription = Clean(request.FurnishingDescription);
        property.AdditionalFeesDescription = Clean(request.AdditionalFeesDescription);
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
        await db.Entry(property).Collection(x => x.PropertyAmenities).Query().Include(x => x.Amenity).LoadAsync(cancellationToken);
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

        // Không cho ẩn/đóng tin khi hợp đồng đang có hiệu lực; bảo vệ quyền lợi người thuê.
        var hasActiveContract = await db.RentalContracts.AnyAsync(x =>
            x.PropertyId == id &&
            x.Status == RentalContractStatus.Active &&
            x.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) &&
            x.EndDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)),
            cancellationToken);
        if (hasActiveContract && request.Status != PropertyListingStatus.Published)
            return Conflict(new { message = "Căn nhà đang có hợp đồng thuê hiệu lực. Không thể ẩn hoặc đóng tin đăng cho đến khi hợp đồng kết thúc." });

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
        RequireModerationAfterImageChange(property);
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
        RequireModerationAfterImageChange(property);
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

        RequireModerationAfterImageChange(property);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void RequireModerationAfterImageChange(Property property)
    {
        // Thay đổi hình ảnh cũng là thay đổi nội dung tin đăng, cần Admin duyệt lại.
        property.ModerationStatus = PropertyModerationStatus.Pending;
        property.ListingStatus = PropertyListingStatus.Draft;
        property.UpdatedAt = DateTime.UtcNow;
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<PropertyResponse> ToResponseAsync(Property property, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var active = await db.RentalContracts.AnyAsync(x =>
            x.PropertyId == property.Id && x.Status == RentalContractStatus.Active &&
            x.StartDate <= today && x.EndDate > today, cancellationToken);
        var pending = await db.Bookings.AnyAsync(x =>
            x.PropertyId == property.Id && x.Status == BookingStatus.Pending &&
            x.EndDate > today, cancellationToken);
        var acceptedFuture = await db.Bookings.AnyAsync(x =>
            x.PropertyId == property.Id && x.Status == BookingStatus.Accepted &&
            x.StartDate > today && x.EndDate > today, cancellationToken);
        var rentalStatus = active ? "Rented" : acceptedFuture ? "Reserved" : pending ? "PendingRequest" : "Available";
        return ToResponse(property, rentalStatus);
    }

    private static PropertyResponse ToResponse(Property property, string rentalStatus) =>
        new(
            property.Id,
            property.LandlordId,
            property.PropertyTypeId,
            property.PropertyType?.Name ?? string.Empty,
            property.Title,
            property.Description,
            property.PricePerMonth,
            property.DepositAmount,
            property.MaxOccupants,
            property.FurnishingDescription,
            property.AdditionalFeesDescription,
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
            rentalStatus,
            property.CreatedAt,
            property.UpdatedAt,
            property.Images.OrderBy(x => x.DisplayOrder)
                .Select(x => new PropertyImageResponse(x.Id, x.ImageUrl, x.IsPrimary, x.DisplayOrder))
                .ToArray(),
            property.PropertyAmenities.Select(x => x.Amenity.Name).OrderBy(x => x).ToArray());
}
