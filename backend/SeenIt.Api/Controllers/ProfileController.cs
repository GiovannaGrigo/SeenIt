using Microsoft.AspNetCore.RateLimiting;
using SeenIt.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Data;
using SeenIt.Api.Domain;
using SeenIt.Api.DTOs.Profile;
using SeenIt.Api.Services;
using SeenIt.Api.Services.Avatars;
using System.Data.Common;

namespace SeenIt.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(AbuseProtection.Mutation)]
[Route("api/profile")]
public sealed class ProfileController(
    AppDbContext database,
    CurrentUserService currentUser,
    AvatarProfileService avatars,
    IAvatarStorageService avatarStorage,
    IPasswordHasher<User> passwordHasher)
    : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(AbuseProtection.Read)]
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
    [EnableRateLimiting(AbuseProtection.Expensive)]
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
        catch (CatalogNotFoundException)
        {
            return NotFound(new { message = "Uma série do histórico não está mais disponível no catálogo." });
        }
        catch (Exception error) when (error is CatalogException or System.Text.Json.JsonException or HttpRequestException || error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Não foi possível consultar a duração dos episódios. Tente novamente." });
        }
    }

    [HttpGet("stats")]
    [EnableRateLimiting(AbuseProtection.Read)]
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

        try
        {
            await avatars.DeleteAccountAsync(user.Id, user.AvatarFileName, user.PasswordHash, cancellationToken);
            return NoContent();
        }
        catch (AvatarConflictException error) { return Conflict(new { message = error.Message }); }
        catch (Exception error) when (error is DbException or DbUpdateException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Não foi possível atualizar o perfil. Tente novamente." });
        }
    }

    [HttpPut("avatar")]
    [EnableRateLimiting(AbuseProtection.Avatar)]
    [RequestSizeLimit(AvatarImageProcessor.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AvatarImageProcessor.MaxFileBytes)]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateAvatar([FromForm] IFormFile avatar, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken);
        if (user is null) return Unauthorized();
        try
        {
            user.AvatarFileName = await avatars.ReplaceAsync(user.Id, user.AvatarFileName, avatar, cancellationToken);
            return Ok(ToResponse(user));
        }
        catch (InvalidAvatarException error) { return BadRequest(new { message = error.Message }); }
        catch (AvatarConflictException error) { return Conflict(new { message = error.Message }); }
        catch (AvatarStorageException error) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = error.Message }); }
        catch (Exception error) when (error is DbException or DbUpdateException or IOException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Não foi possível atualizar o avatar. Tente novamente." });
        }
    }

    [HttpDelete("avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAvatar(CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken);
        if (user is null) return Unauthorized();
        try
        {
            await avatars.RemoveAsync(user.Id, user.AvatarFileName, cancellationToken);
            return NoContent();
        }
        catch (AvatarConflictException error) { return Conflict(new { message = error.Message }); }
        catch (Exception error) when (error is DbException or DbUpdateException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Não foi possível atualizar o avatar. Tente novamente." });
        }
    }

    private ProfileResponse ToResponse(User user) => new(
        user.Id, user.Name, user.Email,
        avatarStorage.GetPublicUrl(user.Id, user.AvatarFileName, Request));
}
