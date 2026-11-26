using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
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
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminUsersController(AppDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    /// <summary>Tạo tài khoản do quản trị viên khởi tạo và gán vai trò.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminUserDetail), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            return Conflict(new { message = "Email này đã được sử dụng." });

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
            return BadRequest(new { message = "Vai trò không hợp lệ. Chỉ dùng Admin, Landlord hoặc Tenant." });

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { User = user, RoleId = role.Id });

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetUser), new { userId = user.Id },
            new AdminUserDetail(user.Id, user.FullName, user.Email, user.PhoneNumber,
                user.AvatarUrl, user.IsActive, user.CreatedAt, user.UpdatedAt,
                roles.Select(x => x.Name).ToArray()));
    }

    /// <summary>Danh sách tài khoản, hỗ trợ tìm kiếm, lọc trạng thái và phân trang.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Phân trang tại SQL Server; tham số sai phải báo 400 thay vì âm thầm đổi giá trị.
        if (page < 1 || pageSize < 1 || pageSize > 100 || page > int.MaxValue / pageSize)
            return BadRequest(new { message = "page phải từ 1; pageSize phải từ 1 đến 100." });

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

        if(!string.IsNullOrWhiteSpace(role))
        {
            if(role is not ("Admin" or "Landlord" or "Tenant"))return BadRequest(new{message="Vai trò không hợp lệ."});
            query=query.Where(x=>x.UserRoles.Any(r=>r.Role.Name==role));
        }
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var users = page > totalPages
            ? []
            : await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
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
            totalPages,
            hasPrevious = page > 1 && page <= totalPages,
            hasNext = page < totalPages,
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

    /// <summary>Cập nhật thông tin hồ sơ tài khoản; email đăng nhập không thay đổi.</summary>
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateUser(
        Guid userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            return NotFound(new { message = "Không tìm thấy tài khoản." });

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var roles = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Select(x => x.Role.Name)
            .ToArrayAsync(cancellationToken);

        return Ok(new AdminUserDetail(user.Id, user.FullName, user.Email, user.PhoneNumber,
            user.AvatarUrl, user.IsActive, user.CreatedAt, user.UpdatedAt, roles));
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

        // Serializable bảo vệ quy tắc không khóa quản trị viên cuối cùng khi có request đồng thời.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);

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

        // Serializable bảo vệ việc thu hồi quyền Admin cuối cùng trước các request cập nhật role đồng thời.
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message = "Đã cập nhật vai trò. Người dùng cần đăng nhập lại để JWT nhận vai trò mới.",
            userId = user.Id,
            roles = roles.Select(x => x.Name).ToArray()
        });
    }

    /// <summary>Xóa mềm tài khoản bằng cách ngừng hoạt động, giữ lịch sử giao dịch và quan hệ dữ liệu.</summary>
    [HttpDelete("{userId:guid}")]
    public Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken) =>
        UpdateStatus(userId, new UpdateUserStatusRequest { IsActive = false }, cancellationToken);

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
