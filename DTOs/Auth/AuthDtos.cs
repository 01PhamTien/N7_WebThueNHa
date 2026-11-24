using System.ComponentModel.DataAnnotations;

namespace Web2_ThueNha.DTOs.Auth;

public sealed class RegisterRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), StringLength(100)]
    public string Password { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles);

public sealed record CurrentUserResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    IReadOnlyCollection<string> Roles);

public sealed class UpdateProfileRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Url, StringLength(1000)]
    public string? AvatarUrl { get; set; }
}
