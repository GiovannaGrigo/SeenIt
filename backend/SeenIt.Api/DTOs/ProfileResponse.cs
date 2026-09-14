namespace SeenIt.Api.DTOs.Profile;

public sealed record ProfileResponse(
    Guid Id,
    string Name,
    string Email,
    string? AvatarUrl
);