using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Appointments;

// Covers the donor-facing confirm/decline/reschedule flow for
// PendingConfirmation appointments — the state an agent-dispatched
// appointment starts in now instead of going straight to Scheduled
// (AppointmentService.CreateAsync), and the explicit accept/decline it
// waits for (ConfirmAsync/DeclineAsync/RescheduleAsync).
public sealed class AppointmentConfirmDeclineRescheduleTests : IDisposable
{
    private static readonly Guid OrgId = Guid.Parse("0c000000-0000-0000-0000-000000000001");
    private static readonly Guid DonorA = Guid.Parse("0c000000-0000-0000-0000-000000000010");
    private static readonly Guid DonorB = Guid.Parse("0c000000-0000-0000-0000-000000000011");

    private static readonly Guid PendingAppt = Guid.Parse("0c000000-0000-0000-0000-000000000020");
    private static readonly Guid ScheduledAppt = Guid.Parse("0c000000-0000-0000-0000-000000000021");
    private static readonly Guid CancelledAppt = Guid.Parse("0c000000-0000-0000-0000-000000000022");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AppointmentConfirmDeclineRescheduleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var ctx = new TestAppDbContext(_options);
        ctx.Database.EnsureCreated();
        Seed(ctx);
    }

    public void Dispose() => _connection.Dispose();

    private AppointmentService NewService(TestAppDbContext ctx) =>
        new(ctx, new InventoryService(ctx));

    // ---------------------------------------------------------------
    // Confirm
    // ---------------------------------------------------------------

    [Fact]
    public async Task ConfirmAsync_PendingAppointment_BecomesScheduled()
    {
        await using var ctx = new TestAppDbContext(_options);

        var result = await NewService(ctx).ConfirmAsync(PendingAppt, DonorA);

        Assert.Equal("scheduled", result.Status);

        await using var verify = new TestAppDbContext(_options);
        var stored = await verify.DonationAppointments.SingleAsync(a => a.Id == PendingAppt);
        Assert.Equal(AppointmentStatus.Scheduled, stored.Status);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyScheduledAppointment_Throws()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(ctx).ConfirmAsync(ScheduledAppt, DonorA));
    }

    [Fact]
    public async Task ConfirmAsync_NotTheOwningDonor_Throws()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewService(ctx).ConfirmAsync(PendingAppt, DonorB));
    }

    [Fact]
    public async Task ConfirmAsync_UnknownAppointment_ThrowsNotFound()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => NewService(ctx).ConfirmAsync(Guid.NewGuid(), DonorA));
    }

    // ---------------------------------------------------------------
    // Decline
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeclineAsync_PendingAppointment_BecomesDeclined()
    {
        await using var ctx = new TestAppDbContext(_options);

        var result = await NewService(ctx).DeclineAsync(PendingAppt, DonorA);

        Assert.Equal("declined", result.Status);
    }

    [Fact]
    public async Task DeclineAsync_NotTheOwningDonor_Throws()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewService(ctx).DeclineAsync(PendingAppt, DonorB));
    }

    [Fact]
    public async Task DeclineAsync_DeclinedAppointmentExcludedFromOrgUpcomingList()
    {
        await using var ctx = new TestAppDbContext(_options);
        var service = NewService(ctx);

        await service.DeclineAsync(PendingAppt, DonorA);

        await using var verify = new TestAppDbContext(_options);
        var page = await NewService(verify).GetUpcomingByOrganizationAsync(
            OrgId, page: 1, pageSize: 20, requestingUserId: Guid.NewGuid(), isAdmin: true);

        Assert.DoesNotContain(page.Items, i => i.Id == PendingAppt);
    }

    // ---------------------------------------------------------------
    // Reschedule
    // ---------------------------------------------------------------

    [Fact]
    public async Task RescheduleAsync_PendingAppointment_MovesTimeAndBecomesScheduled()
    {
        await using var ctx = new TestAppDbContext(_options);
        var newTime = DateTime.UtcNow.AddDays(3);

        var result = await NewService(ctx).RescheduleAsync(PendingAppt, DonorA, newTime);

        Assert.Equal("scheduled", result.Status);
        Assert.Equal(newTime, result.ScheduledTime);
    }

    [Fact]
    public async Task RescheduleAsync_AlreadyScheduledAppointment_MovesTime()
    {
        await using var ctx = new TestAppDbContext(_options);
        var newTime = DateTime.UtcNow.AddDays(5);

        var result = await NewService(ctx).RescheduleAsync(ScheduledAppt, DonorA, newTime);

        Assert.Equal("scheduled", result.Status);
        Assert.Equal(newTime, result.ScheduledTime);
    }

    [Fact]
    public async Task RescheduleAsync_PastTime_Throws()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewService(ctx).RescheduleAsync(ScheduledAppt, DonorA, DateTime.UtcNow.AddDays(-1)));
    }

    [Fact]
    public async Task RescheduleAsync_CancelledAppointment_Throws()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(ctx).RescheduleAsync(
                CancelledAppt, DonorA, DateTime.UtcNow.AddDays(2)));
    }

    [Fact]
    public async Task RescheduleAsync_NotTheOwningDonor_Throws()
    {
        await using var ctx = new TestAppDbContext(_options);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewService(ctx).RescheduleAsync(
                ScheduledAppt, DonorB, DateTime.UtcNow.AddDays(2)));
    }

    private static void Seed(TestAppDbContext ctx)
    {
        ctx.Organizations.Add(new Organization
        {
            Id = OrgId,
            Name = "Test Blood Bank",
            Type = OrganizationType.BloodBank,
            Address = "1 Test Rd",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.Users.AddRange(NewUser(DonorA), NewUser(DonorB));

        var soon = DateTime.UtcNow.AddDays(1);
        ctx.DonationAppointments.AddRange(
            NewAppointment(PendingAppt, DonorA, soon, AppointmentStatus.PendingConfirmation),
            NewAppointment(ScheduledAppt, DonorA, soon.AddHours(1), AppointmentStatus.Scheduled),
            NewAppointment(CancelledAppt, DonorA, soon.AddHours(2), AppointmentStatus.Cancelled));

        ctx.SaveChanges();
    }

    private static User NewUser(Guid id) => new()
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

    private static DonationAppointment NewAppointment(
        Guid id, Guid donorId, DateTime when, AppointmentStatus status) => new()
    {
        Id = id,
        DonorId = donorId,
        OrganizationId = OrgId,
        ScheduledTime = when,
        Status = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
