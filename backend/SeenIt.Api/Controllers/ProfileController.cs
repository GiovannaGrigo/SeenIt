using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.DTOs.Profile;
using SeenIt.Api.Services;

namespace SeenIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(
    AppDbContext database,
    CurrentUserService currentUser,
    IWebHostEnvironment environment,
    IPasswordHasher<User> passwordHasher)
    : ControllerBase
{
    private const long MaxAvatarSize = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    [HttpGet]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var user = await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == currentUser.UserId,
                cancellationToken
            );

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(ToResponse(user));
    }

    [HttpGet("watch-time")]
    public async Task<ActionResult<WatchTimeResponse>> GetWatchTime(
        [FromQuery] string timeZone,
        [FromServices] WatchTimeService watchTime,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return BadRequest(new { message = "Fuso horário inválido." });
        }

        try { return Ok(await watchTime.GetAsync(currentUser.UserId, zone, cancellationToken)); }
        catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Não foi possível consultar a duração dos episódios. Tente novamente." });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var series = await database.UserSeries.CountAsync(x => x.UserId == userId, cancellationToken);
        var episodes = await database.EpisodiosAssistidos.CountAsync(x => x.UserId == userId, cancellationToken);
        var favorites = await database.UserSeries.CountAsync(x => x.UserId == userId && x.IsFavorite, cancellationToken);
        return Ok(new { series, episodes, favorites });
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken);
        if (user is null) return Unauthorized();

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Senha atual incorreta." });
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new { message = "A nova senha deve ser diferente da senha atual." });
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new
            {
                message = "O nome é obrigatório."
            });
        }

        if (name.Length > 120)
        {
            return BadRequest(new
            {
                message = "O nome deve possuir no máximo 120 caracteres."
            });
        }

        var user = await database.Users.SingleOrDefaultAsync(
            x => x.Id == currentUser.UserId,
            cancellationToken
        );

        if (user is null)
        {
            return Unauthorized();
        }

        user.Name = name;

        await database.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(user));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExcluirConta([FromBody] DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "Informe sua senha para excluir a conta."
            });
        }

        var user = await database.Users.SingleOrDefaultAsync(
            user => user.Id == currentUser.UserId,
            cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var resultadoSenha = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (resultadoSenha == PasswordVerificationResult.Failed)
        {
            return BadRequest(new
            {
                message = "Senha incorreta."
            });
        }

        var avatarFileName = user.AvatarFileName;

        database.Users.Remove(user);

        await database.SaveChangesAsync(cancellationToken);

        var directory = Path.Combine(
            environment.WebRootPath,
            "uploads",
            "profiles");

        DeleteAvatarFile(directory, avatarFileName);

        return NoContent();
    }

    [HttpPut("avatar")]
    [RequestSizeLimit(MaxAvatarSize)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateAvatar([FromForm] IFormFile avatar, CancellationToken cancellationToken)
    {
        if (avatar is null || avatar.Length == 0)
        {
            return BadRequest(new
            {
                message = "Selecione uma imagem."
            });
        }

        if (avatar.Length > MaxAvatarSize)
        {
            return BadRequest(new
            {
                message = "A imagem deve possuir no máximo 5 MB."
            });
        }

        if (!AllowedTypes.TryGetValue(
                avatar.ContentType,
                out var extension))
        {
            return BadRequest(new
            {
                message = "Envie uma imagem JPG, PNG ou WEBP."
            });
        }

        var user = await database.Users.SingleOrDefaultAsync(
            x => x.Id == currentUser.UserId,
            cancellationToken
        );

        if (user is null)
        {
            return Unauthorized();
        }

        var directory = Path.Combine(
            environment.WebRootPath,
            "uploads",
            "profiles"
        );

        Directory.CreateDirectory(directory);

        var newFileName = $"{Guid.NewGuid():N}{extension}";
        var newFilePath = Path.Combine(directory, newFileName);

        await using (var stream = System.IO.File.Create(newFilePath))
        {
            await avatar.CopyToAsync(stream, cancellationToken);
        }

        var oldFileName = user.AvatarFileName;

        user.AvatarFileName = newFileName;

        await database.SaveChangesAsync(cancellationToken);

        DeleteAvatarFile(directory, oldFileName);

        return Ok(ToResponse(user));
    }

    [HttpDelete("avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(
            x => x.Id == currentUser.UserId,
            cancellationToken
        );

        if (user is null)
        {
            return Unauthorized();
        }

        var oldFileName = user.AvatarFileName;

        user.AvatarFileName = null;

        await database.SaveChangesAsync(cancellationToken);

        var directory = Path.Combine(
            environment.WebRootPath,
            "uploads",
            "profiles"
        );

        DeleteAvatarFile(directory, oldFileName);

        return NoContent();
    }

    private ProfileResponse ToResponse(User user)
    {
        var avatarUrl = string.IsNullOrWhiteSpace(user.AvatarFileName)
            ? null
            : $"{Request.Scheme}://{Request.Host}/uploads/profiles/{user.AvatarFileName}";

        return new ProfileResponse(
            user.Id,
            user.Name,
            user.Email,
            avatarUrl
        );
    }

    private static void DeleteAvatarFile(
        string directory,
        string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var safeFileName = Path.GetFileName(fileName);
        var filePath = Path.Combine(directory, safeFileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}
