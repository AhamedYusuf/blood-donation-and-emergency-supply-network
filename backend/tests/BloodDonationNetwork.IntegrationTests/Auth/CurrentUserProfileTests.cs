using BloodDonationNetwork.Application.DTOs.Auth;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Auth;

public sealed class CurrentUserProfileTests : IDisposable
{
    private static readonly Guid UserId =
        Guid.Parse("0c000000-0000-0000-0000-000000000001");

    private static readonly Guid OrganizationId =
        Guid.Parse("0c000000-0000-0000-0000-000000000002");

    private static readonly Guid AdminId =
        Guid.Parse("0c000000-0000-0000-0000-000000000003");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CurrentUserProfileTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
        context.Organizations.Add(new Organization
        {
            Id = OrganizationId,
            Name = "Test organization",
            Type = OrganizationType.Hospital,
            Address = "Test address",
            PhoneNumber = "0000000000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        context.Users.Add(new User
        {
            Id = UserId,
            Email = "staff@example.com",
            PasswordHash = "not-a-real-hash",
            Role = UserRole.Staff,
            FullName = "Test Staff",
            PhoneNumber = "0000000000",
            OrganizationId = OrganizationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsDatabaseOrganizationAssignment()
    {
        await using var context = NewContext();
        var service = new AuthService(context, new UnusedJwtTokenService());

        var currentUser = await service.GetCurrentUserAsync(UserId);

        Assert.NotNull(currentUser);
        Assert.Equal(UserId, currentUser.UserId);
        Assert.Equal("Staff", currentUser.Role);
        Assert.Equal(OrganizationId, currentUser.OrganizationId);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNullForUnknownUser()
    {
        await using var context = NewContext();
        var service = new AuthService(context, new UnusedJwtTokenService());

        var currentUser =
            await service.GetCurrentUserAsync(Guid.NewGuid());

        Assert.Null(currentUser);
    }

    [Fact]
    public async Task AcceptInvitation_AssignsOrganizationReturnedByCurrentUserLookup()
    {
        await using (var context = NewContext())
        {
            context.Users.Add(new User
            {
                Id = AdminId,
                Email = "admin@example.com",
                PasswordHash = "not-a-real-hash",
                Role = UserRole.Admin,
                FullName = "Test Admin",
                PhoneNumber = "0000000000",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            context.StaffInvitations.Add(new StaffInvitation
            {
                Email = "invited-staff@example.com",
                OrganizationId = OrganizationId,
                InvitedByUserId = AdminId,
                Token = "invitation-token",
                ExpiresAt = DateTime.UtcNow.AddDays(1),
            });
            await context.SaveChangesAsync();
        }

        AuthResponse accepted;
        await using (var context = NewContext())
        {
            var invitationService = new StaffInvitationService(
                context,
                new UnusedJwtTokenService());

            accepted = await invitationService.AcceptAsync(
                new AcceptStaffInvitationRequest
                {
                    Token = "invitation-token",
                    Password = "test-password-123",
                    FullName = "Invited Staff",
                    PhoneNumber = "0000000000",
                });
        }

        await using var verificationContext = NewContext();
        var currentUser = await new AuthService(
                verificationContext,
                new UnusedJwtTokenService())
            .GetCurrentUserAsync(accepted.UserId);

        Assert.Equal(OrganizationId, accepted.OrganizationId);
        Assert.NotNull(currentUser);
        Assert.Equal(OrganizationId, currentUser.OrganizationId);
    }

    public void Dispose() => _connection.Dispose();

    private TestAppDbContext NewContext() => new(_options);

    private sealed class UnusedJwtTokenService : IJwtTokenService
    {
        public string GenerateAccessToken(
            Guid userId,
            string email,
            string role,
            Guid? organizationId) =>
            "test-access-token";

        public string GenerateRefreshToken() =>
            "test-refresh-token";
    }
}
