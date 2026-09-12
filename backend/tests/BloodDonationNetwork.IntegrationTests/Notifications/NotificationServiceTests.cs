using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Notifications;

// Covers the Tech Doc §0.10 behaviours for push delivery: fan-out to a
// donor's devices, bounded retry on transient failure, pruning of dead
// tokens, and never throwing when a donor can't be reached.
public sealed class NotificationServiceTests : IDisposable
{
    private static readonly Guid Donor = Guid.Parse("0c000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherDonor = Guid.Parse("0c000000-0000-0000-0000-000000000002");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var ctx = new TestAppDbContext(_options);
        ctx.Database.EnsureCreated();

        // DonorDevices.DonorUserId now has a real FK to Users.Id — every
        // device row below needs a matching user to exist first.
        ctx.Users.AddRange(SeedUser(Donor), SeedUser(OtherDonor));
        ctx.SaveChanges();
    }

    private static User SeedUser(Guid id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        PasswordHash = "not-a-real-hash",
        Role = UserRole.Donor,
        FullName = "Test Donor",
        PhoneNumber = "0000000000",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    public void Dispose() => _connection.Dispose();

    private TestAppDbContext NewCtx() => new(_options);

    private static DonorDevice Device(Guid donor, string token, string platform = "android") => new()
    {
        Id = Guid.NewGuid(),
        DonorUserId = donor,
        FcmToken = token,
        Platform = platform,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        LastSeenAt = DateTime.UtcNow.AddDays(-1),
    };

    private sealed class FakeFcm : IFcmSender
    {
        public FcmSendOutcome Default = FcmSendOutcome.Delivered;
        public Dictionary<string, FcmSendOutcome> ByToken { get; } = new();
        public List<string> Calls { get; } = new();

        public Task<FcmSendOutcome> SendAsync(
            string deviceToken, string title, string body,
            IReadOnlyDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(deviceToken);
            return Task.FromResult(ByToken.TryGetValue(deviceToken, out var o) ? o : Default);
        }
    }

    [Fact]
    public async Task SendToDonor_WithNoDevices_ReturnsNoDevices_AndDoesNotThrow()
    {
        await using var ctx = NewCtx();
        var sut = new NotificationService(ctx, new FakeFcm());

        var result = await sut.SendToDonorAsync(Donor, "t", "b");

        Assert.Equal(0, result.DevicesTried);
        Assert.False(result.AnyDelivered);
        Assert.Equal("Donor has no registered devices", result.FailureReason);
    }

    [Fact]
    public async Task SendToDonor_DeliversToEveryDeviceAndBumpsLastSeen()
    {
        await using (var seed = NewCtx())
        {
            seed.DonorDevices.AddRange(Device(Donor, "a"), Device(Donor, "b"), Device(OtherDonor, "z"));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewCtx();
        var fcm = new FakeFcm { Default = FcmSendOutcome.Delivered };
        var result = await new NotificationService(ctx, fcm).SendToDonorAsync(Donor, "t", "b");

        Assert.Equal(2, result.DevicesTried);
        Assert.Equal(2, result.Delivered);
        Assert.True(result.AnyDelivered);
        Assert.DoesNotContain("z", fcm.Calls); // other donor untouched

        await using var verify = NewCtx();
        var bumped = await verify.DonorDevices
            .Where(d => d.DonorUserId == Donor)
            .AllAsync(d => d.LastSeenAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(bumped);
    }

    [Fact]
    public async Task SendToDonor_PrunesTokensFcmReportsAsInvalid()
    {
        await using (var seed = NewCtx())
        {
            seed.DonorDevices.AddRange(Device(Donor, "good"), Device(Donor, "dead"));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewCtx();
        var fcm = new FakeFcm();
        fcm.ByToken["good"] = FcmSendOutcome.Delivered;
        fcm.ByToken["dead"] = FcmSendOutcome.InvalidToken;

        var result = await new NotificationService(ctx, fcm).SendToDonorAsync(Donor, "t", "b");

        Assert.Equal(1, result.Delivered);
        Assert.Equal(1, result.InvalidTokensPruned);

        await using var verify = NewCtx();
        Assert.False(await verify.DonorDevices.AnyAsync(d => d.FcmToken == "dead"));
        Assert.True(await verify.DonorDevices.AnyAsync(d => d.FcmToken == "good"));
    }

    [Fact]
    public async Task SendToDonor_RetriesTransientFailureUpToTheCap_ThenGivesUpWithoutPruning()
    {
        await using (var seed = NewCtx())
        {
            seed.DonorDevices.Add(Device(Donor, "flaky"));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewCtx();
        var fcm = new FakeFcm { Default = FcmSendOutcome.TransientFailure };

        var result = await new NotificationService(ctx, fcm).SendToDonorAsync(Donor, "t", "b");

        Assert.Equal(NotificationService.MaxAttemptsPerDevice, fcm.Calls.Count);
        Assert.False(result.AnyDelivered);
        Assert.Equal(0, result.InvalidTokensPruned);

        await using var verify = NewCtx();
        Assert.True(await verify.DonorDevices.AnyAsync(d => d.FcmToken == "flaky"));
    }

    [Fact]
    public async Task SendToDonor_WhenSenderNotConfigured_ShortCircuits()
    {
        await using (var seed = NewCtx())
        {
            seed.DonorDevices.AddRange(Device(Donor, "a"), Device(Donor, "b"));
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewCtx();
        var fcm = new FakeFcm { Default = FcmSendOutcome.NotConfigured };

        var result = await new NotificationService(ctx, fcm).SendToDonorAsync(Donor, "t", "b");

        Assert.Single(fcm.Calls); // stopped after the first device
        Assert.False(result.AnyDelivered);
        Assert.Equal("Push notifications are not configured", result.FailureReason);
    }

    [Fact]
    public async Task RegisterDevice_AddsNew_ThenUpsertsOnReRegistration()
    {
        await using (var ctx = NewCtx())
        {
            await new NotificationService(ctx, new FakeFcm())
                .RegisterDeviceAsync(Donor, "tok-1", "ios");
        }
        await using (var ctx = NewCtx())
        {
            // same token, different donor + platform
            await new NotificationService(ctx, new FakeFcm())
                .RegisterDeviceAsync(OtherDonor, "tok-1", "android");
        }

        await using var verify = NewCtx();
        var rows = await verify.DonorDevices.Where(d => d.FcmToken == "tok-1").ToListAsync();
        Assert.Single(rows);
        Assert.Equal(OtherDonor, rows[0].DonorUserId);
        Assert.Equal("android", rows[0].Platform);
    }

    [Fact]
    public async Task UnregisterDevice_RemovesOnlyTheCallersToken()
    {
        await using (var seed = NewCtx())
        {
            seed.DonorDevices.AddRange(Device(Donor, "mine"), Device(OtherDonor, "theirs"));
            await seed.SaveChangesAsync();
        }

        await using (var ctx = NewCtx())
        {
            var svc = new NotificationService(ctx, new FakeFcm());
            await svc.UnregisterDeviceAsync(Donor, "theirs"); // not the caller's — no-op
            await svc.UnregisterDeviceAsync(Donor, "mine");
        }

        await using var verify = NewCtx();
        Assert.False(await verify.DonorDevices.AnyAsync(d => d.FcmToken == "mine"));
        Assert.True(await verify.DonorDevices.AnyAsync(d => d.FcmToken == "theirs"));
    }
}
