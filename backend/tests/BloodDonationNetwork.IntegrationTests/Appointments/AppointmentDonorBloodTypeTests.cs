using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Appointments;

// Task 8: AppointmentResponseDto.DonorBloodType is joined from the donor's
// DonorProfile so the staff console can show it without fabricating data.
// These cover the read paths (single, per-donor list, per-org batch) and
// the "no profile yet" null case.
public sealed class AppointmentDonorBloodTypeTests : IDisposable
{
    private static readonly Guid OrgId = Guid.Parse("0b000000-0000-0000-0000-000000000001");
    private static readonly Guid DonorWithProfile = Guid.Parse("0b000000-0000-0000-0000-000000000010");
    private static readonly Guid DonorOtherType = Guid.Parse("0b000000-0000-0000-0000-000000000011");
    private static readonly Guid DonorNoProfile = Guid.Parse("0b000000-0000-0000-0000-000000000012");

    private static readonly Guid ApptWithProfile = Guid.Parse("0b000000-0000-0000-0000-000000000020");
    private static readonly Guid ApptOtherType = Guid.Parse("0b000000-0000-0000-0000-000000000021");
    private static readonly Guid ApptNoProfile = Guid.Parse("0b000000-0000-0000-0000-000000000022");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AppointmentDonorBloodTypeTests()
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

    [Fact]
    public async Task GetByIdAsync_ReturnsTheDonorsBloodType()
    {
        await using var ctx = new TestAppDbContext(_options);

        var result = await NewService(ctx).GetByIdAsync(ApptWithProfile);

        Assert.NotNull(result);
        Assert.Equal("O+", result!.DonorBloodType);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullBloodType_WhenDonorHasNoProfile()
    {
        await using var ctx = new TestAppDbContext(_options);

        var result = await NewService(ctx).GetByIdAsync(ApptNoProfile);

        Assert.NotNull(result);
        Assert.Null(result!.DonorBloodType);
    }

    [Fact]
    public async Task GetByDonorAsync_TagsEveryRowWithThatDonorsBloodType()
    {
        await using var ctx = new TestAppDbContext(_options);

        var results = await NewService(ctx).GetByDonorAsync(DonorOtherType);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("AB-", r.DonorBloodType));
    }

    [Fact]
    public async Task GetUpcomingByOrganizationAsync_MapsBloodTypePerDonor_IncludingNulls()
    {
        await using var ctx = new TestAppDbContext(_options);

        var page = await NewService(ctx).GetUpcomingByOrganizationAsync(
            OrgId, page: 1, pageSize: 20, requestingUserId: Guid.NewGuid(), isAdmin: true);

        string? BloodTypeFor(Guid apptId) =>
            page.Items.Single(i => i.Id == apptId).DonorBloodType;

        Assert.Equal("O+", BloodTypeFor(ApptWithProfile));
        Assert.Equal("AB-", BloodTypeFor(ApptOtherType));
        Assert.Null(BloodTypeFor(ApptNoProfile));
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

        ctx.DonorProfiles.AddRange(
            NewDonorProfile(DonorWithProfile, "O+"),
            NewDonorProfile(DonorOtherType, "AB-"));

        var soon = DateTime.UtcNow.AddHours(6);
        ctx.DonationAppointments.AddRange(
            NewAppointment(ApptWithProfile, DonorWithProfile, soon),
            NewAppointment(ApptOtherType, DonorOtherType, soon.AddHours(1)),
            NewAppointment(ApptNoProfile, DonorNoProfile, soon.AddHours(2)));

        ctx.SaveChanges();
    }

    private static DonorProfile NewDonorProfile(Guid userId, string bloodType) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        BloodType = bloodType,
        DateOfBirth = new DateOnly(1990, 1, 1),
        VerifiedByAdmin = true,
        ReliabilityScore = 1.0m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static DonationAppointment NewAppointment(Guid id, Guid donorId, DateTime when) => new()
    {
        Id = id,
        DonorId = donorId,
        OrganizationId = OrgId,
        ScheduledTime = when,
        Status = AppointmentStatus.Scheduled,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
