using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ZipZap.Modules.Identity.Application;
using ZipZap.Modules.Identity.Domain;

namespace ZipZap.Modules.Identity.Infrastructure;

/// <summary>Wystawia tokeny JWT (HS256) i losowe refresh tokeny (hash SHA-256).</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Role.ToString()));
            if (role.StoreId is not Guid storeId) continue;

            // `store_id` = sklep ZARZĄDZANY (pracownik) → jedyna podstawa ManagesStore/StoreIds.
            // Kierowca dostaje osobny claim: przypisanie do sklepu NIE daje uprawnień zarządczych
            // (inaczej kierowca sklepu A przechodziłby ManagesStore(A), a pracownik B + kierowca A
            // mógłby zarządzać sklepem A).
            if (role.Role == Role.StoreEmployee)
                claims.Add(new Claim("store_id", storeId.ToString()));
            else if (role.Role == Role.Driver)
                claims.Add(new Claim("driver_store_id", storeId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            IssuedAt = now,
            NotBefore = now,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expires);
    }

    public RefreshTokenValue CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expires = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
        return new RefreshTokenValue(raw, HashRefreshToken(raw), expires);
    }

    public string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
