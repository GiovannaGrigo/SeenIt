using System.ComponentModel.DataAnnotations;

namespace SeenIt.Api.DTOs;

public sealed partial record RegisterRequest(
    [Required, MaxLength(120)] string? Name,
    [Required, EmailAddress, MaxLength(200)] string? Email,
    [Required, MinLength(8), MaxLength(128)] string? Password);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, UserResponse User);
public sealed record UserResponse(Guid Id, string Name, string Email, string? AvatarUrl);
