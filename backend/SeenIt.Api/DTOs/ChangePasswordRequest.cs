using System.ComponentModel.DataAnnotations;

namespace SeenIt.Api.DTOs.Profile;

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword
);
