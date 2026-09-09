using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Tests;

public class InventoryServiceTests
{
    [Fact]
    public async Task DonationIn_IncreasesStockAndCreatesTransaction()
    {
        await using var db = CreateDb();

        var inventory = CreateInventory(5);
        db.BloodBankInventories.Add(inventory);
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.AdjustInventoryAsync(
            inventory.Id,
            new AdjustInventoryRequest(
                3,
                InventoryTransactionType.DonationIn));

        Assert.Equal(8, result.UnitsAvailable);

        var transaction = await db.InventoryTransactions.SingleAsync();

        Assert.Equal(
            InventoryTransactionType.DonationIn,
            transaction.TransactionType);

        Assert.Equal(3, transaction.Units);
    }

    [Fact]
    public async Task UsageOut_DecreasesStock()
    {
        await using var db = CreateDb();

        var inventory = CreateInventory(10);
        db.BloodBankInventories.Add(inventory);
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.AdjustInventoryAsync(
            inventory.Id,
            new AdjustInventoryRequest(
                4,
                InventoryTransactionType.UsageOut));

        Assert.Equal(6, result.UnitsAvailable);
    }

    [Fact]
    public async Task UsageOut_CannotCreateNegativeStock()
    {
        await using var db = CreateDb();

        var inventory = CreateInventory(2);
        db.BloodBankInventories.Add(inventory);
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustInventoryAsync(
                inventory.Id,
                new AdjustInventoryRequest(
                    5,
                    InventoryTransactionType.UsageOut)));
    }

    [Fact]
    public async Task AdjustInventory_RejectsZeroUnits()
    {
        await using var db = CreateDb();

        var inventory = CreateInventory(5);
        db.BloodBankInventories.Add(inventory);
        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AdjustInventoryAsync(
                inventory.Id,
                new AdjustInventoryRequest(
                    0,
                    InventoryTransactionType.DonationIn)));
    }

    [Fact]
    public async Task GetLowStock_ReturnsLowStockItems()
    {
        await using var db = CreateDb();

        var organizationId = Guid.NewGuid();

        db.BloodBankInventories.AddRange(
            new BloodBankInventory
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BloodType = BloodType.OPositive,
                UnitsAvailable = 3,
                LowStockThreshold = 5,
                LastUpdated = DateTime.UtcNow
            },
            new BloodBankInventory
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                BloodType = BloodType.APositive,
                UnitsAvailable = 10,
                LowStockThreshold = 5,
                LastUpdated = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.GetLowStockAsync(organizationId);

        Assert.Single(result);
        Assert.Equal(BloodType.OPositive, result[0].BloodType);
    }

    [Fact]
    public async Task CheckStock_ReturnsSufficient_WhenEnoughStockExists()
    {
        await using var db = CreateDb();

        var organizationId = Guid.NewGuid();

        db.BloodBankInventories.Add(new BloodBankInventory
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            BloodType = BloodType.OPositive,
            UnitsAvailable = 10,
            LowStockThreshold = 5,
            LastUpdated = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.CheckStockAsync(
            new StockCheckRequest(
                organizationId,
                BloodType.OPositive,
                6));

        Assert.True(result.Sufficient);
        Assert.Equal(10, result.AvailableUnits);
        Assert.Equal(4, result.RemainingUnits);
    }

    [Fact]
    public async Task CheckStock_ReturnsInsufficient_WhenStockIsLow()
    {
        await using var db = CreateDb();

        var organizationId = Guid.NewGuid();

        db.BloodBankInventories.Add(new BloodBankInventory
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            BloodType = BloodType.ONegative,
            UnitsAvailable = 3,
            LowStockThreshold = 5,
            LastUpdated = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.CheckStockAsync(
            new StockCheckRequest(
                organizationId,
                BloodType.ONegative,
                5));

        Assert.False(result.Sufficient);
        Assert.Equal(3, result.AvailableUnits);
        Assert.Equal(0, result.RemainingUnits);
    }

    [Fact]
    public async Task EmergencyRecommendation_ReturnsProceed_WhenEnoughStockExists()
    {
        await using var db = CreateDb();

        db.BloodBankInventories.Add(new BloodBankInventory
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BloodType = BloodType.APositive,
            UnitsAvailable = 20,
            LowStockThreshold = 5,
            LastUpdated = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.GetEmergencyRecommendationAsync(
            new EmergencyInventoryRequest(
                BloodType.APositive,
                10,
                RequestUrgency.Critical));

        Assert.Equal(20, result.AvailableUnits);
        Assert.Equal(0, result.Shortfall);
        Assert.Equal("SUFFICIENT_STOCK", result.Recommendation);
        Assert.Equal(
            "RESERVE_STOCK_AND_PROCEED",
            result.SuggestedAction);
    }

    [Fact]
    public async Task EmergencyRecommendation_ReturnsTransfer_WhenStockIsInsufficient()
    {
        await using var db = CreateDb();

        db.BloodBankInventories.Add(new BloodBankInventory
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BloodType = BloodType.OPositive,
            UnitsAvailable = 5,
            LowStockThreshold = 5,
            LastUpdated = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new InventoryService(db);

        var result = await service.GetEmergencyRecommendationAsync(
            new EmergencyInventoryRequest(
                BloodType.OPositive,
                10,
                RequestUrgency.Critical));

        Assert.Equal(5, result.AvailableUnits);
        Assert.Equal(5, result.Shortfall);
        Assert.Equal("PARTIAL_STOCK", result.Recommendation);
        Assert.Equal(
            "USE_AVAILABLE_STOCK_AND_REQUEST_EMERGENCY_TRANSFER",
            result.SuggestedAction);
    }

    private static TestApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    private static BloodBankInventory CreateInventory(int units)
    {
        return new BloodBankInventory
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BloodType = BloodType.OPositive,
            UnitsAvailable = units,
            LowStockThreshold = 5,
            LastUpdated = DateTime.UtcNow
        };
    }

    private sealed class TestApplicationDbContext
        : DbContext, IApplicationDbContext
    {
        public TestApplicationDbContext(
            DbContextOptions<TestApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organization> Organizations =>
            Set<Organization>();

        public DbSet<BloodBankInventory> BloodBankInventories =>
            Set<BloodBankInventory>();

        public DbSet<InventoryTransaction> InventoryTransactions =>
            Set<InventoryTransaction>();

        public DbSet<DonorProfile> DonorProfiles =>
            throw new NotSupportedException();

        public DbSet<User> Users =>
            throw new NotSupportedException();

        public DbSet<DonationAppointment> DonationAppointments =>
            throw new NotSupportedException();

        public DbSet<BloodRequest> BloodRequests =>
            throw new NotSupportedException();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            // Do not include unrelated entities in the test model.
            modelBuilder.Ignore<DonorProfile>();
            modelBuilder.Ignore<User>();
            modelBuilder.Ignore<DonationAppointment>();
            modelBuilder.Ignore<BloodRequest>();

            modelBuilder.Entity<BloodBankInventory>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Organization)
                    .WithMany()
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.BloodType)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(x => x.UnitsAvailable)
                    .IsRequired();

                entity.Property(x => x.LowStockThreshold)
                    .IsRequired();

                entity.Property(x => x.LastUpdated)
                    .IsRequired();
            });

            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasOne(x => x.Inventory)
                    .WithMany(x => x.Transactions)
                    .HasForeignKey(x => x.InventoryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(x => x.TransactionType)
                    .HasConversion<string>()
                    .IsRequired();

                entity.Property(x => x.Units)
                    .IsRequired();

                entity.Property(x => x.CreatedAt)
                    .IsRequired();
            });
        }
    }
}