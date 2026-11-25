using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Admin;

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
