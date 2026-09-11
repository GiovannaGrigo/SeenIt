using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SeenIt.Api.Domain;

namespace SeenIt.Api.Services;

public sealed class TokenService(IConfiguration configuration)
{
    public (string Token, DateTime ExpiresAtUtc) Create(User user)
    {
        var expirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes");
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key não foi configurada.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
