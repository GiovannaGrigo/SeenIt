using System.Security.Claims;

namespace SeenIt.Api.Services;

public sealed class CurrentUserService(IHttpContextAccessor accessor)
{
    public Guid UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? accessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException("Usuário não autenticado.");
        }
    }
}
