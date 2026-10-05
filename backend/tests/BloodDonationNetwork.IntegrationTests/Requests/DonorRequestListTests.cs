using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodDonationNetwork.IntegrationTests.Requests;

// A donor's "requests near you" list must follow the same rule as the
// notifications: eligible to donate, compatible blood type, request still active.
public sealed class DonorRequestListTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestAppDbContext _context;
    private readonly RequestService _service;
    private readonly Guid _donorUserId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    public DonorRequestListTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new TestAppDbContext(options);
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        _service = new RequestService(_context, new NoOpNotificationService(), NullLogger<RequestService>.Instance);
    }

    [Fact]
    public async Task EligibleDonor_SeesOnlyActiveRequestsTheirBloodTypeCanServe()
    {
        await AddDonorAsync(BloodTypes.APositive, verified: true);
        var open = await AddRequestAsync(BloodType.APositive, RequestStatuses.Open);
        var awaiting = await AddRequestAsync(BloodType.ABPositive, RequestStatuses.AwaitingApproval);
        await AddRequestAsync(BloodType.OPositive, RequestStatuses.Open);          // A+ can't give to O+
        await AddRequestAsync(BloodType.APositive, RequestStatuses.Fulfilled);
        await AddRequestAsync(BloodType.APositive, RequestStatuses.Cancelled);

        var visible = await ListForDonorAsync();

        Assert.Equal(new[] { open, awaiting }.OrderBy(id => id), visible.OrderBy(id => id));
    }

    [Fact]
    public async Task DonorWhoIsNotEligibleYet_SeesNoRequests()
    {
        await AddDonorAsync(BloodTypes.APositive, verified: false);
        await AddRequestAsync(BloodType.APositive, RequestStatuses.Open);

        Assert.Empty(await ListForDonorAsync());
    }

    private async Task<Guid[]> ListForDonorAsync()
    {
        var result = await _service.GetAllAsync(
            _donorUserId, UserRole.Donor, nearLat: 0, nearLng: 0, radiusKm: 50, donorUserId: _donorUserId);
        return result.Select(request => request.Id).ToArray();
    }

    private async Task AddDonorAsync(string bloodType, bool verified)
    {
        _context.DonorProfiles.Add(new DonorProfile
        {
            Id = Guid.NewGuid(),
            UserId = _donorUserId,
            BloodType = bloodType,
            DateOfBirth = new DateOnly(1990, 1, 1),
            VerifiedByAdmin = verified,
            Latitude = 0,
            Longitude = 0
        });
        await _context.SaveChangesAsync();
    }

    private async Task<Guid> AddRequestAsync(BloodType bloodType, string status)
    {
        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            OrganizationId = _organizationId,
            BloodType = bloodType,
            UnitsRequested = 1,
            Urgency = RequestUrgency.Normal,
            Status = status,
            HospitalName = "Test Hospital",
            Latitude = 0,
            Longitude = 0.1,
            CreatedAt = DateTime.UtcNow
        };
        _context.BloodRequests.Add(request);
        await _context.SaveChangesAsync();
        return request.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
