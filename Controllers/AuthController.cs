using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Web2_ThueNha.Data;
using Web2_ThueNha.DTOs.Auth;

namespace Web2_ThueNha.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext db,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    /// <summary>Đăng ký tài khoản mới. Tài khoản đăng ký công khai luôn nhận vai trò Tenant.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _db.Users.AnyAsync(x => x.Email == email, cancellationToken);
        if (emailExists)
            return Conflict(new { message = "Email này đã được sử dụng." });

        var tenantRole = await _db.Roles.SingleOrDefaultAsync(x => x.Name == "Tenant", cancellationToken);
        if (tenantRole is null)
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Chưa có dữ liệu vai trò Tenant. Hãy kiểm tra dữ liệu danh mục." });

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        user.UserRoles.Add(new UserRole
        {
            User = user,
            RoleId = tenantRole.Id
        });

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Đăng ký tài khoản thành công.",
            userId = user.Id,
            fullName = user.FullName,
            email = user.Email,
            role = tenantRole.Name
        });
    }

    /// <summary>Đăng nhập bằng email và mật khẩu để nhận JWT Access Token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác." });

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Email hoặc mật khẩu không chính xác." });

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        var roles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        var expiresAt = DateTime.UtcNow.AddHours(2);
        var token = CreateAccessToken(user, roles, expiresAt);

        return Ok(new AuthResponse(
            token,
            expiresAt,
            user.Id,
            user.FullName,
            user.Email,
            roles));
    }

    /// <summary>Lấy thông tin tài khoản từ JWT đang đăng nhập.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (user is null)
            return Unauthorized();

        return Ok(new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.AvatarUrl,
            user.UserRoles.Select(x => x.Role.Name).ToArray()));
    }


    /// <summary>Cập nhật hồ sơ của tài khoản đang đăng nhập.</summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (user is null)
            return Unauthorized();

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl)
                ? null
                : request.AvatarUrl.Trim();
        }
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.AvatarUrl,
            user.UserRoles.Select(x => x.Role.Name).ToArray()));
    }

    private string CreateAccessToken(User user, IEnumerable<string> roles, DateTime expiresAt)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Key.");

        var issuer = _configuration["Jwt:Issuer"] ?? "Web2_ThueNha";
        var audience = _configuration["Jwt:Audience"] ?? "Web2_ThueNhaClient";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
