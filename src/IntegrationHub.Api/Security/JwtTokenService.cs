using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IntegrationHub.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationHub.Api.Security;

public sealed class JwtTokenService(IConfiguration configuration, TimeProvider timeProvider) : IJwtTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) Create(AuthenticatedUser user)
    {
        var encodedKey = configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey must be set using user-secrets or an environment variable.");
        var key = Convert.FromBase64String(encodedKey);
        if (key.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must contain at least 256 bits.");
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 30));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id), new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.FullName), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"] ?? "IntegrationHub.Local",
            configuration["Jwt:Audience"] ?? "IntegrationHub.Web", claims,
            notBefore: timeProvider.GetUtcNow().UtcDateTime, expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
