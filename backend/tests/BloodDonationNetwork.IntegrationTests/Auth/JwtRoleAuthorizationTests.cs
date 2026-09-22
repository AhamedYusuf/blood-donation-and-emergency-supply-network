using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BloodDonationNetwork.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace BloodDonationNetwork.IntegrationTests.Auth;

/// <summary>
/// Regression test for a real bug found while making the JWT role claim
/// key spec-compliant (Tech Doc §0.4 requires the key to be literally
/// "role", not ClaimTypes.Role's XML-namespace URI).
///
/// A first version of that fix explicitly set
/// TokenValidationParameters.RoleClaimType = "role" plus
/// JwtBearerOptions.MapInboundClaims = false in Program.cs. It built
/// clean and every existing unit/integration test passed, because none
/// of them exercise the real ASP.NET Core JWT bearer pipeline - they all
/// call services directly against a hand-built ClaimsPrincipal. Live
/// testing against a running backend showed that "fix" actually broke
/// authorization for every single role: staff and admin both got 403
/// from their own correctly-authorized routes, because disabling the
/// default inbound claim map to fix the role claim also stops "sub" from
/// mapping onto ClaimTypes.NameIdentifier, which several controllers use
/// for ownership checks.
///
/// The actual fix needed no Program.cs change at all: ASP.NET Core's
/// default inbound claim map already renames an incoming "role" claim
/// back onto ClaimTypes.Role automatically, which is also
/// TokenValidationParameters.RoleClaimType's own default - so issuing
/// the claim as "role" (satisfying the spec's requirement on the raw
/// token bytes) keeps every existing User.IsInRole()/[Authorize(Roles=)]
/// check working unchanged.
///
/// This test boots a minimal real host with the exact JWT bearer
/// configuration Program.cs uses (no RoleClaimType/MapInboundClaims
/// override), issues tokens with the real JwtTokenService, and sends
/// them through real HTTP requests against [Authorize]-protected
/// endpoints. It intentionally does not boot the full Program.cs/API
/// host, since that eagerly builds a real Npgsql data source from a
/// connection string and would pull in the whole app's DB/FCM/seed
/// startup logic - none of which this test is about. What it does share
/// with Program.cs is the only two things this bug actually depends on:
/// the AddJwtBearer TokenValidationParameters block, and the real
/// JwtTokenService that issues the token.
/// </summary>
public class JwtRoleAuthorizationTests : IAsyncLifetime
{
    private const string Issuer = "BloodDonationNetwork";
    private const string Audience = "BloodDonationNetworkUsers";
    private const string SigningKey = "test-only-signing-key-must-be-at-least-32-bytes-long";

    private IHost _host = null!;
    private HttpClient _client = null!;
    private JwtTokenService _tokenService = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:Key"] = SigningKey,
            })
            .Build();

        _tokenService = new JwtTokenService(configuration);

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();

                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();

                    // Mirrors Program.cs's AddJwtBearer block exactly -
                    // no RoleClaimType or MapInboundClaims override.
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = Issuer,
                                ValidAudience = Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(SigningKey)),
                            };
                        });

                    services.AddAuthorization();
                });

                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/test/admin-only", () => Results.Ok())
                            .RequireAuthorization(new AuthorizeAttribute { Roles = "admin" });

                        endpoints.MapGet("/test/staff-only", () => Results.Ok())
                            .RequireAuthorization(new AuthorizeAttribute { Roles = "staff" });

                        endpoints.MapGet("/test/whoami", (ClaimsPrincipal user) =>
                                Results.Ok(new
                                {
                                    UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                                }))
                            .RequireAuthorization();
                    });
                });
            });

        _host = await hostBuilder.StartAsync();
        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    private HttpRequestMessage AuthedRequest(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public void IssuedToken_RoleClaim_HasLiteralRoleKey()
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), "admin@example.com", "Admin", null);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var roleClaim = Assert.Single(jwt.Claims, c => c.Type == "role");
        Assert.Equal("admin", roleClaim.Value);
    }

    [Fact]
    public async Task AdminToken_CanAccessAdminOnlyEndpoint()
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), "admin@example.com", "Admin", null);

        var response = await _client.SendAsync(AuthedRequest("/test/admin-only", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Donor")]
    public async Task NonAdminToken_IsRejectedFromAdminOnlyEndpoint(string role)
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), "user@example.com", role, null);

        var response = await _client.SendAsync(AuthedRequest("/test/admin-only", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffToken_CanAccessStaffOnlyEndpoint()
    {
        var token = _tokenService.GenerateAccessToken(
            Guid.NewGuid(), "staff@example.com", "Staff", Guid.NewGuid());

        var response = await _client.SendAsync(AuthedRequest("/test/staff-only", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminToken_IsRejectedFromStaffOnlyEndpoint()
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), "admin@example.com", "Admin", null);

        var response = await _client.SendAsync(AuthedRequest("/test/staff-only", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_IsRejectedWithUnauthorized()
    {
        var response = await _client.GetAsync("/test/admin-only");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IssuedToken_SubClaim_StillMapsToNameIdentifier()
    {
        // This is the other half of the regression: disabling inbound
        // claim mapping to fix the role claim also silently breaks every
        // ClaimTypes.NameIdentifier-based ownership check (donor viewing
        // their own appointments, staff completing their own org's
        // appointment, etc.) across the app - none of which showed up as
        // a role/permission failure, but as a NullReferenceException or
        // ArgumentNullException deep in a controller trying to Guid.Parse
        // a claim that was no longer there.
        var userId = Guid.NewGuid();
        var token = _tokenService.GenerateAccessToken(userId, "donor@example.com", "Donor", null);

        var response = await _client.SendAsync(AuthedRequest("/test/whoami", token));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(userId.ToString(), body.GetProperty("userId").GetString());
    }
}
