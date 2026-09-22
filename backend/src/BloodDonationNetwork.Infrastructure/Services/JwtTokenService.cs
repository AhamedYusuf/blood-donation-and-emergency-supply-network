using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BloodDonationNetwork.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(Guid userId, string email, string role, Guid? organizationId)
    {
        var key = _configuration["Jwt:Key"]
                  ?? throw new InvalidOperationException(
                      "JWT key is not configured.");

        var issuer = _configuration["Jwt:Issuer"]
                     ?? throw new InvalidOperationException(
                         "JWT issuer is not configured.");

        var audience = _configuration["Jwt:Audience"]
                       ?? throw new InvalidOperationException(
                           "JWT audience is not configured.");

        var expiresValue = _configuration["Jwt:ExpiresInMinutes"];

        var expiresInMinutes = 60;

        if (int.TryParse(expiresValue, out var configuredMinutes))
        {
            expiresInMinutes = configuredMinutes;
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            // Tech Doc §0.4 requires the claim key to be literally "role"
            // in the raw JWT payload, not ClaimTypes.Role's full
            // XML-namespace URI. No other change is needed: ASP.NET
            // Core's JWT bearer handler auto-maps an inbound "role" claim
            // back onto ClaimsPrincipal's standard role claim type by
            // default (JwtSecurityTokenHandler.DefaultInboundClaimTypeMap),
            // which is also RoleClaimType's default — so
            // User.IsInRole()/[Authorize(Roles=...)] keep working exactly
            // as before. (Explicitly setting RoleClaimType/MapInboundClaims
            // in Program.cs was tried and reverted — it broke every
            // ClaimTypes.NameIdentifier-based ownership check across the
            // app, since disabling inbound mapping to fix "role" also
            // stops "sub" from mapping to NameIdentifier.)
            new("role", role.ToLowerInvariant()),
        };

        // Tech Doc §0.4: organizationId claim, present only for users that
        // belong to an organization (staff).
        if (organizationId.HasValue)
        {
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));
        }

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }
}