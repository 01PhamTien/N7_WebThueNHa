using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Admin;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminUsersController(AppDbContext db) => _db = db;

    /// <summary>Danh sách tài khoản, hỗ trợ tìm kiếm, lọc trạng thái và phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                x.FullName.Contains(term) ||
                x.Email.Contains(term) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(term)));
        }

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var totalItems = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminUserListItem(
                x.Id,
                x.FullName,
                x.Email,
                x.PhoneNumber,
                x.AvatarUrl,
                x.IsActive,
                x.CreatedAt,
                x.UserRoles.Select(ur => ur.Role.Name).ToArray()))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            page,
            pageSize,
            totalItems,
            totalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
            items = users
        });
    }

    /// <summary>Xem chi tiết một tài khoản, không trả về PasswordHash.</summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new AdminUserDetail(
                x.Id,
                x.FullName,
                x.Email,
                x.PhoneNumber,
                x.AvatarUrl,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt,
                x.UserRoles.Select(ur => ur.Role.Name).ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? NotFound(new { message = "Không tìm thấy tài khoản." }) : Ok(user);
    }

    /// <summary>Khóa hoặc mở khóa tài khoản. Không cho phép tự khóa tài khoản đang đăng nhập.</summary>
    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        if (currentUserId == userId && !request.IsActive)
            return BadRequest(new { message = "Bạn không thể tự khóa tài khoản đang đăng nhập." });

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
            return NotFound(new { message = "Không tìm thấy tài khoản." });

        if (user.IsActive && !request.IsActive && user.UserRoles.Any(x => x.Role.Name == "Admin"))
        {
            var activeAdminCount = await _db.Users
                .CountAsync(x => x.IsActive && x.UserRoles.Any(ur => ur.Role.Name == "Admin"), cancellationToken);
            if (activeAdminCount <= 1)
                return Conflict(new { message = "Không thể khóa quản trị viên cuối cùng đang hoạt động." });
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = request.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.", userId, user.IsActive });
    }

    /// <summary>Cập nhật vai trò tài khoản. Chỉ chấp nhận các vai trò đã có trong bảng Roles.</summary>
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> UpdateRoles(
        Guid userId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        if (currentUserId == userId)
            return BadRequest(new { message = "Không thể tự thay đổi vai trò của chính mình. Hãy nhờ một quản trị viên khác thực hiện." });

        if (request.Roles is null)
            return BadRequest(new { message = "Danh sách vai trò không được để trống." });

        var requestedNames = request.Roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedNames.Length == 0)
            return BadRequest(new { message = "Tài khoản phải có ít nhất một vai trò." });

        var roles = await _db.Roles
            .Where(x => requestedNames.Contains(x.Name))
            .ToListAsync(cancellationToken);

        if (roles.Count != requestedNames.Length)
            return BadRequest(new { message = "Có vai trò không hợp lệ. Chỉ dùng Admin, Landlord hoặc Tenant đã được cấu hình." });

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
            return NotFound(new { message = "Không tìm thấy tài khoản." });

        var wasAdmin = user.UserRoles.Any(x => x.Role.Name == "Admin");
        var willBeAdmin = roles.Any(x => x.Name == "Admin");

        if (wasAdmin && !willBeAdmin)
        {
            var activeAdminCount = await _db.Users
                .CountAsync(x => x.IsActive && x.UserRoles.Any(ur => ur.Role.Name == "Admin"), cancellationToken);
            if (user.IsActive && activeAdminCount <= 1)
                return Conflict(new { message = "Không thể gỡ vai trò Admin của quản trị viên cuối cùng đang hoạt động." });
        }

        // Chỉ thêm/xóa phần chênh lệch để tránh tracking conflict khi role giữ nguyên.
        var requestedRoleIds = roles.Select(x => x.Id).ToHashSet();
        var currentRoleIds = user.UserRoles.Select(x => x.RoleId).ToHashSet();

        var rolesToRemove = user.UserRoles
            .Where(x => !requestedRoleIds.Contains(x.RoleId))
            .ToArray();
        _db.UserRoles.RemoveRange(rolesToRemove);

        foreach (var role in roles.Where(x => !currentRoleIds.Contains(x.Id)))
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Đã cập nhật vai trò. Người dùng cần đăng nhập lại để JWT nhận vai trò mới.",
            userId = user.Id,
            roles = roles.Select(x => x.Name).ToArray()
        });
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
