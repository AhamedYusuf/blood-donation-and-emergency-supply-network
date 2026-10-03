using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Inventory;

// AdjustInventoryAsync/CreateTransactionAsync used to load the tracked
// BloodBankInventory row, mutate UnitsAvailable in memory, then
// SaveChangesAsync — a classic read-modify-write. Two real concurrent
// requests touching the same row (e.g. two staff completing appointments
// for the same blood type at once) could both read the same starting
// value and both "succeed," with the second SaveChangesAsync silently
// overwriting the first's result. This proves the fix: the stock delta
// is now applied via a single atomic UPDATE ... SET UnitsAvailable =
// UnitsAvailable + @delta statement, so both concurrent deltas always
// land regardless of how the two calls interleave.
public sealed class InventoryConcurrencyTests : IAsyncLifetime
{
    private const string DataSource =
        "Data Source=file:inventory-concurrency-test;Mode=Memory;Cache=Shared";

    private SqliteConnection _keepAliveConnection = null!;
    private DbContextOptions<AppDbContext> _options = null!;

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _inventoryId = Guid.NewGuid();
    private readonly Guid _adminUserId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        // A shared-cache SQLite in-memory database is dropped the instant
        // its last open connection closes — this connection is kept open
        // for the test's lifetime purely to keep the database alive while
        // the two DbContexts below use their own separate connections to
        // it, the same way two real HTTP requests would each get their
        // own scoped DbContext/connection against the same Postgres DB.
        _keepAliveConnection = new SqliteConnection(DataSource);
        await _keepAliveConnection.OpenAsync();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DataSource)
            .Options;

        await using var ctx = new TestAppDbContext(_options);
        await ctx.Database.EnsureCreatedAsync();

        ctx.Organizations.Add(new Organization
        {
            Id = _organizationId,
            Name = "Concurrency Test Org",
            Type = OrganizationType.BloodBank,
            Address = "1 Test Rd",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.Users.Add(new User
        {
            Id = _adminUserId,
            Email = "admin@example.com",
            PasswordHash = "not-a-real-hash",
            Role = UserRole.Admin,
            FullName = "Admin",
            PhoneNumber = "0000000000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.BloodBankInventories.Add(new BloodBankInventory
        {
            Id = _inventoryId,
            OrganizationId = _organizationId,
            BloodType = BloodType.OPositive,
            UnitsAvailable = 10,
            LowStockThreshold = 5,
            LastUpdated = DateTime.UtcNow,
        });

        await ctx.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _keepAliveConnection.DisposeAsync();

    [Fact]
    public async Task AdjustInventory_IgnoresAStaleTrackedValue_AndUsesTheRowsCommittedValue()
    {
        // ctxB stands in for a second, concurrent request's DbContext.
        // Querying the row here is what a real request would do on its
        // way in (e.g. loading it earlier for a different check) —
        // afterwards, EF Core's change tracker holds this instance and
        // will keep handing it back for any later by-key query on this
        // SAME context, even after another context commits a change to
        // the underlying row. This is a concrete, deterministic way to
        // reproduce "the value this request is working from is stale" —
        // the same hazard a real interleaved concurrent write causes,
        // without depending on real thread timing or SQLite's specific
        // locking behaviour, which earlier testing showed masks a true
        // multi-threaded race in ways Postgres would not.
        await using var ctxB = new TestAppDbContext(_options);
        _ = await ctxB.BloodBankInventories
            .FirstAsync(x => x.Id == _inventoryId);

        // A genuinely separate request (ctxA) now commits its own +5
        // change and completes fully.
        await using (var ctxA = new TestAppDbContext(_options))
        {
            var serviceA = new InventoryService(ctxA);

            await serviceA.AdjustInventoryAsync(
                _inventoryId,
                new AdjustInventoryRequest(5, InventoryTransactionType.DonationIn),
                _adminUserId);
        }

        // ctxB now applies its own +3 change. Its change tracker still
        // holds the row as it stood BEFORE ctxA's commit (UnitsAvailable
        // == 10), which is exactly what a read-modify-write
        // implementation would compute from: 10 + 3 = 13, silently
        // discarding ctxA's +5. The atomic UPDATE this method now runs
        // instead computes UnitsAvailable + 3 against the row's actual
        // committed value (15) at write time, regardless of what ctxB's
        // own tracker believes that value is.
        var serviceB = new InventoryService(ctxB);

        await serviceB.AdjustInventoryAsync(
            _inventoryId,
            new AdjustInventoryRequest(3, InventoryTransactionType.DonationIn),
            _adminUserId);

        await using var verifyCtx = new TestAppDbContext(_options);

        var finalInventory = await verifyCtx.BloodBankInventories
            .AsNoTracking()
            .FirstAsync(x => x.Id == _inventoryId);

        // 10 (seeded) + 5 (ctxA) + 3 (ctxB) = 18. A read-modify-write
        // implementation would leave this at 13, having overwritten
        // ctxA's +5 with a value computed from ctxB's stale read.
        Assert.Equal(18, finalInventory.UnitsAvailable);
    }
}
