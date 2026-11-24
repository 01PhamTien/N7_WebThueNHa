using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Contracts;
using Web2_ThueNha.Models;

using Web2_ThueNha.Services;
using Web2_ThueNha.Services.Contracts;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Roles = "Landlord,Tenant")]
public sealed class ContractsController(AppDbContext db, IContractService contractService) : ControllerBase
{
    private const int MaxPageSize = 50;

    /// <summary>Danh sách hợp đồng của tài khoản hiện tại, lọc và phân trang tại SQL Server.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedContractsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetContracts(
        [FromQuery] RentalContractStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize ||
            page > int.MaxValue / pageSize ||
            (status.HasValue && !Enum.IsDefined(status.Value)))
            return BadRequest(new { message = "Tham số lọc hoặc phân trang không hợp lệ." });

        var isLandlord = User.IsInRole("Landlord");
        var isTenant = User.IsInRole("Tenant");
        IQueryable<RentalContract> query = db.RentalContracts.AsNoTracking();

        if (isLandlord && isTenant)
            query = query.Where(x => x.LandlordId == userId || x.TenantId == userId);
        else if (isLandlord)
            query = query.Where(x => x.LandlordId == userId);
        else
            query = query.Where(x => x.TenantId == userId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = page > totalPages
            ? new List<ContractListItem>()
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ContractListItem(
                    x.Id,
                    x.ContractCode,
                    x.BookingId,
                    x.PropertyId,
                    x.Property.Title,
                    x.LandlordId,
                    x.Landlord.FullName,
                    x.TenantId,
                    x.Tenant.FullName,
                    x.StartDate,
                    x.EndDate,
                    x.MonthlyRent,
                    x.DepositAmount,
                    x.Status.ToString(),
                    x.CreatedAt))
                .ToListAsync(cancellationToken);

        return Ok(new PagedContractsResponse(
            items,
            page,
            pageSize,
            totalItems,
            totalPages,
            page > 1 && page <= totalPages,
            page < totalPages));
    }

    /// <summary>Xem chi tiết hợp đồng chỉ khi tài khoản hiện tại là Tenant hoặc Landlord tham gia.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RentalContractDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContract(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var contract = await db.RentalContracts
            .AsNoTracking()
            .Include(x => x.Property)
            .Include(x => x.Landlord)
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(
                x => x.Id == id && (x.LandlordId == userId || x.TenantId == userId),
                cancellationToken);

        if (contract is null)
            return NotFound(new { message = "Không tìm thấy hợp đồng." });

        return Ok(new RentalContractDetailResponse(
            ToResponse(contract),
            contract.Property.Title,
            contract.Landlord.FullName,
            contract.Tenant.FullName));
    }

    /// <summary>Tạo hợp đồng nháp từ Booking Accepted; Tenant hoặc Landlord tham gia đều có thể khởi tạo.</summary>
    [HttpPost("from-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> public async Task<IActionResult> CreateFromBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await contractService.CreateFromBookingAsync(bookingId, userId, cancellationToken);
        if (result.StatusCode == StatusCodes.Status201Created && result.Data is not null)
            return CreatedAtAction(nameof(GetContract), new { id = result.Data.Id }, result.Data);

        return ToActionResult(result);
    }

    /// <summary>Chủ nhà chỉnh sửa nội dung hợp đồng khi hợp đồng còn Draft.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> public async Task<IActionResult> UpdateContract(
        Guid id,
        [FromBody] UpdateRentalContractRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return ToActionResult(await contractService.UpdateAsync(id, userId, request, cancellationToken));
    }

    /// <summary>Chủ nhà gửi bản nháp cho hai bên xác nhận; không tự chuyển hợp đồng sang Active.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> public async Task<IActionResult> SubmitForSignature(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return ToActionResult(await contractService.SubmitForSignatureAsync(id, userId, cancellationToken));
    }


    /// <summary>Mỗi bên chỉ xác nhận một lần; hợp đồng chỉ Active khi cả Landlord và Tenant đã ký.</summary>
    [HttpPost("{id:guid}/sign")]
    [ProducesResponseType(typeof(RentalContractResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> public async Task<IActionResult> SignContract(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        return ToActionResult(await contractService.SignAsync(id, userId, cancellationToken));
    }


    private IActionResult ToActionResult<T>(ServiceResult<T> result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Data),
            StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
            StatusCodes.Status403Forbidden => Forbid(),
            StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
            StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
            _ => StatusCode(result.StatusCode, result.Message is null ? null : new { message = result.Message })
        };

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static RentalContractResponse ToResponse(RentalContract contract) =>
        new(
            contract.Id,
            contract.BookingId,
            contract.PropertyId,
            contract.LandlordId,
            contract.TenantId,
            contract.ContractCode,
            contract.StartDate,
            contract.EndDate,
            contract.MonthlyRent,
            contract.DepositAmount,
            contract.PaymentDueDay,
            contract.Terms,
            contract.ContractUrl,
            contract.Status.ToString(),
            contract.LandlordSignedAt,
            contract.TenantSignedAt,
            contract.SignedAt,
            contract.CreatedAt);
}
