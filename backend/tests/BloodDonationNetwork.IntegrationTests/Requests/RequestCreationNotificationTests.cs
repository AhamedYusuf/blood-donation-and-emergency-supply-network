using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Notifications;
using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodDonationNetwork.IntegrationTests.Requests;

public sealed class RequestCreationNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestAppDbContext _context;
    private readonly RecordingNotificationService _notifications = new();
    private readonly RequestService _service;

    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    public RequestCreationNotificationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new TestAppDbContext(options);
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

        _service = new RequestService(
            _context,
            _notifications,
            NullLogger<RequestService>.Instance);
    }

    [Fact]
    public async Task Create_NotifiesDonorWithin50Km_WithExpectedNotificationData()
    {
        var nearbyDonorId = Guid.NewGuid();
        await AddDonorsAsync(
            CreateDonor(nearbyDonorId, 0, 0.4));

        var request = await CreateRequestAsync();

        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal(nearbyDonorId, notification.DonorUserId);
        Assert.Equal("New Blood Request Near You", notification.Title);
        Assert.Equal(
            "A new A+ blood request is available near you.",
            notification.Body);
        Assert.Equal("new_blood_request", notification.Data["type"]);
        Assert.Equal(request.Id.ToString(), notification.Data["bloodRequestId"]);
    }

    [Fact]
    public async Task Create_DoesNotNotifyDonorOutside50Km()
    {
        await AddDonorsAsync(
            CreateDonor(Guid.NewGuid(), 0, 0.6));

        await CreateRequestAsync();

        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Create_IgnoresDonorsWithNullCoordinates()
    {
        await AddDonorsAsync(
            CreateDonor(Guid.NewGuid(), null, 0),
            CreateDonor(Guid.NewGuid(), 0, null));

        await CreateRequestAsync();

        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Create_AttemptsAllNearbyDonors()
    {
        var donorIds = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        await AddDonorsAsync(
            CreateDonor(donorIds[0], 0.1, 0),
            CreateDonor(donorIds[1], 0, 0.2),
            CreateDonor(donorIds[2], -0.1, 0));

        await CreateRequestAsync();

        Assert.Equal(
            donorIds.OrderBy(id => id),
            _notifications.Sent
                .Select(notification => notification.DonorUserId)
                .OrderBy(id => id));
    }

    [Fact]
    public async Task Create_NotificationFailureDoesNotFailOrUndoRequestCreation()
    {
        var failingDonorId = Guid.NewGuid();
        var succeedingDonorId = Guid.NewGuid();
        _notifications.FailingDonorIds.Add(failingDonorId);
        await AddDonorsAsync(
            CreateDonor(failingDonorId, 0, 0.1),
            CreateDonor(succeedingDonorId, 0, 0.2));

        var request = await CreateRequestAsync();

        Assert.Equal(
            new[] { failingDonorId, succeedingDonorId }.OrderBy(id => id),
            _notifications.Sent
                .Select(notification => notification.DonorUserId)
                .OrderBy(id => id));
        var persistedRequest = await _context.BloodRequests
            .AsNoTracking()
            .SingleAsync(item => item.Id == request.Id);
        Assert.Equal(RequestStatuses.Open, persistedRequest.Status);
    }

    [Fact]
    public async Task Create_PersistsRequestEvenWhenThereAreNoNearbyDonors()
    {
        var request = await CreateRequestAsync();

        var persistedRequest = await _context.BloodRequests
            .AsNoTracking()
            .SingleAsync(item => item.Id == request.Id);

        Assert.Equal(request.Id, persistedRequest.Id);
        Assert.Equal(RequestStatuses.Open, persistedRequest.Status);
        Assert.Empty(_notifications.Sent);
    }

    private async Task AddDonorsAsync(params DonorProfile[] donors)
    {
        _context.DonorProfiles.AddRange(donors);
        await _context.SaveChangesAsync();
    }

    private async Task<BloodDonationNetwork.Application.DTOs.Requests.RequestResponseDto>
        CreateRequestAsync()
    {
        _context.Organizations.Add(new Organization
        {
            Id = _organizationId,
            Name = "Test Hospital",
            Type = OrganizationType.Hospital,
            Address = "Test address",
            Latitude = 0,
            Longitude = 0,
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return await _service.CreateAsync(
            _adminId,
            _adminId,
            isAdmin: true,
            new CreateRequestDto
            {
                OrganizationId = _organizationId,
                BloodType = BloodType.APositive,
                UnitsRequested = 1,
                Urgency = RequestUrgency.Normal,
                Notes = string.Empty
            });
    }

    private static DonorProfile CreateDonor(
        Guid userId,
        double? latitude,
        double? longitude) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BloodType = BloodTypes.ONegative,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Latitude = latitude,
            Longitude = longitude
        };

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<SentNotification> Sent { get; } = [];
        public HashSet<Guid> FailingDonorIds { get; } = [];

        public Task<NotificationResult> SendToDonorAsync(
            Guid donorUserId,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default)
        {
            Sent.Add(new SentNotification(
                donorUserId,
                title,
                body,
                data?.ToDictionary(item => item.Key, item => item.Value)
                    ?? new Dictionary<string, string>()));

            if (FailingDonorIds.Contains(donorUserId))
            {
                return Task.FromResult(
                    new NotificationResult(
                        donorUserId,
                        DevicesTried: 1,
                        Delivered: 0,
                        InvalidTokensPruned: 0,
                        AnyDelivered: false,
                        FailureReason: "Simulated notification failure."));
            }

            return Task.FromResult(
                new NotificationResult(
                    donorUserId,
                    DevicesTried: 1,
                    Delivered: 1,
                    InvalidTokensPruned: 0,
                    AnyDelivered: true));
        }

        public Task RegisterDeviceAsync(
            Guid donorUserId,
            string fcmToken,
            string platform,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UnregisterDeviceAsync(
            Guid donorUserId,
            string fcmToken,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PagedResult<DonorNotificationDto>> GetForDonorAsync(
            Guid donorUserId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<DonorNotificationDto>());

        public Task<int> GetUnreadCountAsync(
            Guid donorUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task MarkAsReadAsync(
            Guid id,
            Guid donorUserId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed record SentNotification(
        Guid DonorUserId,
        string Title,
        string Body,
        Dictionary<string, string> Data);
}
