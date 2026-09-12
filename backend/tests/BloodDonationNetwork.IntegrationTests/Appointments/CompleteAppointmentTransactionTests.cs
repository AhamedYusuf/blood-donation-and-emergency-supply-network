using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainBloodType = BloodDonationNetwork.Domain.Enums.BloodType;

namespace BloodDonationNetwork.IntegrationTests.Appointments;

// Tech Doc §4.3 requires AppointmentService.CompleteAsync to update the
// appointment, write the "donation_in" inventory transaction, and set the
// donor's LastDonationDate as a single atomic unit. Dev Guide step 16:
// "Test the full Complete() cross-table transaction with a forced failure
// partway through to confirm nothing is left in an inconsistent state."
//
// Runs against SQLite in-memory (a real, transaction-capable provider —
// unlike the EF in-memory provider, which silently ignores transactions).
public sealed class CompleteAppointmentTransactionTests : IDisposable
{
    private static readonly Guid OrgId = Guid.Parse("0a000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffUserId = Guid.Parse("0a000000-0000-0000-0000-000000000002");
    private static readonly Guid DonorUserId = Guid.Parse("0a000000-0000-0000-0000-000000000003");
    private static readonly Guid AppointmentId = Guid.Parse("0a000000-0000-0000-0000-000000000004");

    private const int UnitsDonated = 2;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CompleteAppointmentTransactionTests()
    {
        // The connection must stay open for the lifetime of the test — the
        // in-memory database is dropped as soon as the last connection closes.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new TestAppDbContext(_options);
        ctx.Database.EnsureCreated();
        Seed(ctx);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task CompleteAsync_HappyPath_CommitsAppointmentInventoryAndDonorTogether()
    {
        await using var ctx = new TestAppDbContext(_options);
        var sut = new AppointmentService(ctx, new InventoryService(ctx));

        var beforeUtcDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await sut.CompleteAsync(
            AppointmentId,
            new CompleteAppointmentDto { UnitsDonated = UnitsDonated },
            StaffUserId,
            isAdmin: false);
        var afterUtcDate = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Equal("completed", result.Status);
        Assert.Equal("O+", result.DonorBloodType);

        await using var verify = new TestAppDbContext(_options);

        var appointment = await verify.DonationAppointments.SingleAsync(a => a.Id == AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Equal(UnitsDonated, appointment.UnitsDonated);

        var donor = await verify.DonorProfiles.SingleAsync(d => d.UserId == DonorUserId);
        Assert.NotNull(donor.LastDonationDate);
        Assert.InRange(donor.LastDonationDate!.Value, beforeUtcDate, afterUtcDate);

        var inventory = await verify.BloodBankInventories.SingleAsync(
            i => i.OrganizationId == OrgId && i.BloodType == DomainBloodType.OPositive);
        Assert.Equal(UnitsDonated, inventory.UnitsAvailable);

        var txn = await verify.InventoryTransactions.SingleAsync();
        Assert.Equal(InventoryTransactionType.DonationIn, txn.TransactionType);
        Assert.Equal(UnitsDonated, txn.Units);
        Assert.Equal(AppointmentId, txn.RelatedAppointmentId);
    }

    [Fact]
    public async Task CompleteAsync_FailureAfterInventoryWrite_RollsBackEverything()
    {
        await using var ctx = new TestAppDbContext(_options)
        {
            // save #1 is InventoryService persisting the stock row + transaction;
            // save #2 is CompleteAsync persisting the donor + appointment changes.
            FailOnSaveNumber = 2,
        };
        var sut = new AppointmentService(ctx, new InventoryService(ctx));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteAsync(
            AppointmentId,
            new CompleteAppointmentDto { UnitsDonated = UnitsDonated },
            StaffUserId,
            isAdmin: false));

        await using var verify = new TestAppDbContext(_options);

        // Appointment untouched.
        var appointment = await verify.DonationAppointments.SingleAsync(a => a.Id == AppointmentId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Null(appointment.UnitsDonated);

        // Donor's last-donation date untouched.
        var donor = await verify.DonorProfiles.SingleAsync(d => d.UserId == DonorUserId);
        Assert.Null(donor.LastDonationDate);

        // The inventory row and transaction the InventoryService had already
        // saved (inside the same transaction) are gone after the rollback.
        Assert.False(await verify.BloodBankInventories.AnyAsync());
        Assert.False(await verify.InventoryTransactions.AnyAsync());
    }

    private static void Seed(TestAppDbContext ctx)
    {
        ctx.Organizations.Add(new Organization
        {
            Id = OrgId,
            Name = "Test Blood Bank",
            Type = OrganizationType.BloodBank,
            Address = "123 Test St",
            Latitude = 6.9271,
            Longitude = 79.8612,
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.Users.Add(new User
        {
            Id = StaffUserId,
            Email = "staff@test.local",
            PasswordHash = "x",
            Role = UserRole.Staff,
            FullName = "Test Staff",
            PhoneNumber = "000",
            OrganizationId = OrgId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // DonationAppointments.DonorId now has a real FK to Users.Id.
        ctx.Users.Add(new User
        {
            Id = DonorUserId,
            Email = "donor@test.local",
            PasswordHash = "x",
            Role = UserRole.Donor,
            FullName = "Test Donor",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.DonorProfiles.Add(new DonorProfile
        {
            Id = Guid.NewGuid(),
            UserId = DonorUserId,
            BloodType = "O+",
            DateOfBirth = new DateOnly(1990, 1, 1),
            LastDonationDate = null,
            VerifiedByAdmin = true,
            ReliabilityScore = 1.0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.DonationAppointments.Add(new DonationAppointment
        {
            Id = AppointmentId,
            DonorId = DonorUserId,
            OrganizationId = OrgId,
            ScheduledTime = DateTime.UtcNow.AddHours(-1),
            Status = AppointmentStatus.Scheduled,
            UnitsDonated = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.SaveChanges();
    }
}
