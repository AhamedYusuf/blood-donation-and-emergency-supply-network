using BCryptHasher = BCrypt.Net.BCrypt;
using BloodDonationNetwork.Application.DTOs.Auth;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        AppDbContext dbContext,
        IJwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (existingUser != null)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        var role = Enum.Parse<UserRole>(request.Role, true);

        // Staff/admin belong to an organization; donors never do. Honour the
        // organizationId the caller supplied (Bug #2 — this used to be
        // hardcoded to null, so staff could never be linked to an org
        // through the API and had to be SQL-patched).
        // NOTE: self-registering as staff with an arbitrary org is a trust
        // gap the team should close later (admin-created staff, or an
        // approval step). It is honoured here so the flow works end to end.
        var organizationId = role == UserRole.Donor ? null : request.OrganizationId;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCryptHasher.HashPassword(request.Password),
            Role = UserRole.Donor,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync();

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }
var passwordValid = BCryptHasher.Verify(
    request.Password,
    user.PasswordHash);

        if (!passwordValid)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        user.RefreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshTokenExpiryTime =
            DateTime.UtcNow.AddDays(7);

        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await CreateAuthResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

        if (user == null ||
            user.RefreshTokenExpiryTime == null ||
            user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException(
                "Invalid or expired refresh token.");
        }

        user.RefreshToken = _jwtTokenService.GenerateRefreshToken();

        user.RefreshTokenExpiryTime =
            DateTime.UtcNow.AddDays(7);

        user.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await CreateAuthResponseAsync(user);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user)
    {
        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email,
            user.Role.ToString(),
            user.OrganizationId);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = user.RefreshToken ?? string.Empty,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            DonorProfileId = await _dbContext.DonorProfiles
                .Where(profile => profile.UserId == user.Id)
                .Select(profile => (Guid?)profile.Id)
                .FirstOrDefaultAsync()
            OrganizationId = user.OrganizationId
        };
    }
}