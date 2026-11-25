using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Admin;

public sealed class CreateAdminUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Url, StringLength(1000)]
    public string? AvatarUrl { get; set; }

    [Required, MinLength(1)]
    public List<string> Roles { get; set; } = [];
}

public sealed class UpdateAdminUserRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Url, StringLength(1000)]
    public string? AvatarUrl { get; set; }
}

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

public sealed class UpdateUserRolesRequest
{
    [Required, MinLength(1)]
    public List<string> Roles { get; set; } = [];
}

public sealed record AdminUserListItem(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyCollection<string> Roles);

public sealed record AdminUserDetail(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? AvatarUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<string> Roles);
