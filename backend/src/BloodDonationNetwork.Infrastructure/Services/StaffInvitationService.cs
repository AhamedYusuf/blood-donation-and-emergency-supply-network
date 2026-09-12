using System.Security.Cryptography;
using BCryptHasher = BCrypt.Net.BCrypt;
using BloodDonationNetwork.Application.DTOs.Auth;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Services;

public class StaffInvitationService : IStaffInvitationService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;

    public StaffInvitationService(IApplicationDbContext context, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<StaffInvitationResponse> CreateAsync(Guid invitedByAdminId, CreateStaffInvitationRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId);
        if (organization is null)
        {
            throw new KeyNotFoundException("That organization doesn't exist.");
        }

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        if (await _context.StaffInvitations.AnyAsync(i =>
                i.Email == email && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow))
        {
            throw new InvalidOperationException("There's already a pending invitation for this email.");
        }

        var invitation = new StaffInvitation
        {
            Id = Guid.NewGuid(),
            Email = email,
            OrganizationId = organization.Id,
            InvitedByUserId = invitedByAdminId,
            Token = GenerateToken(),
            ExpiresAt = DateTime.UtcNow.Add(InviteLifetime),
            CreatedAt = DateTime.UtcNow,
        };

        _context.StaffInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        return MapToResponse(invitation, organization.Name, includeToken: true);
    }

    public async Task<AuthResponse> AcceptAsync(AcceptStaffInvitationRequest request)
    {
        var invitation = await _context.StaffInvitations
            .FirstOrDefaultAsync(i => i.Token == request.Token);

        if (invitation is null || !invitation.IsUsable)
        {
            throw new InvalidOperationException("This invitation link is invalid or has expired.");
        }

        if (await _context.Users.AnyAsync(u => u.Email == invitation.Email))
        {
            throw new InvalidOperationException("A user with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = invitation.Email,
            PasswordHash = BCryptHasher.HashPassword(request.Password),
            Role = UserRole.Staff,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            OrganizationId = invitation.OrganizationId,
            RefreshToken = _jwtTokenService.GenerateRefreshToken(),
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        invitation.AcceptedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var accessToken = _jwtTokenService.GenerateAccessToken(
            user.Id, user.Email, user.Role.ToString(), user.OrganizationId);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = user.RefreshToken!,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            DonorProfileId = null,
            OrganizationId = user.OrganizationId,
        };
    }

    public async Task<List<StaffInvitationResponse>> GetByOrganizationAsync(Guid organizationId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId);
        if (organization is null)
        {
            throw new KeyNotFoundException("That organization doesn't exist.");
        }

        var invitations = await _context.StaffInvitations
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        // The token is a one-time secret shown only at creation — never
        // re-exposed through the listing endpoint.
        return invitations.Select(i => MapToResponse(i, organization.Name, includeToken: false)).ToList();
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static StaffInvitationResponse MapToResponse(StaffInvitation i, string organizationName, bool includeToken) =>
        new()
        {
            Id = i.Id,
            Email = i.Email,
            OrganizationId = i.OrganizationId,
            OrganizationName = organizationName,
            Token = includeToken ? i.Token : string.Empty,
            ExpiresAt = i.ExpiresAt,
            CreatedAt = i.CreatedAt,
            AcceptedAt = i.AcceptedAt,
        };
}
