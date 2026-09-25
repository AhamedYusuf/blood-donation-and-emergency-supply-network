using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Inventory;

// CheckStockAsync (the "frontend" POST /api/inventory/stock-check route)
// took a caller-supplied OrganizationId and returned that org's exact
// stock count with no ownership check at all — every other endpoint in
// InventoryController enforces the same EnsureOrganizationAccessAsync
// guard this test proves is now applied here too.
public sealed class CheckStockAccessTests : IDisposable
{
    private static readonly Guid OwnOrgId = Guid.Parse("0d000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherOrgId = Guid.Parse("0d000000-0000-0000-0000-000000000002");
    private static readonly Guid StaffUserId = Guid.Parse("0d000000-0000-0000-0000-000000000010");
    private static readonly Guid AdminUserId = Guid.Parse("0d000000-0000-0000-0000-000000000011");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CheckStockAccessTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var ctx = new TestAppDbContext(_options);
        ctx.Database.EnsureCreated();
        Seed(ctx);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Staff_CannotCheckStock_ForADifferentOrganization()
    {
        await using var ctx = new TestAppDbContext(_options);
        var service = new InventoryService(ctx);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.CheckStockAsync(
                new StockCheckRequest(OtherOrgId, BloodType.OPositive, 1),
                StaffUserId));
    }

    [Fact]
    public async Task Staff_CanCheckStock_ForTheirOwnOrganization()
    {
        await using var ctx = new TestAppDbContext(_options);
        var service = new InventoryService(ctx);

        var result = await service.CheckStockAsync(
            new StockCheckRequest(OwnOrgId, BloodType.OPositive, 1),
            StaffUserId);

        Assert.Equal(OwnOrgId, result.OrganizationId);
    }

    [Fact]
    public async Task Admin_CanCheckStock_ForAnyOrganization()
    {
        await using var ctx = new TestAppDbContext(_options);
        var service = new InventoryService(ctx);

        var result = await service.CheckStockAsync(
            new StockCheckRequest(OtherOrgId, BloodType.OPositive, 1),
            AdminUserId);

        Assert.Equal(OtherOrgId, result.OrganizationId);
    }

    private static void Seed(TestAppDbContext ctx)
    {
        ctx.Organizations.AddRange(
            NewOrg(OwnOrgId, "Own Org"),
            NewOrg(OtherOrgId, "Other Org"));

        ctx.Users.AddRange(
            new User
            {
                Id = StaffUserId,
                Email = "staff@example.com",
                PasswordHash = "not-a-real-hash",
                Role = UserRole.Staff,
                OrganizationId = OwnOrgId,
                FullName = "Staff",
                PhoneNumber = "0000000000",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new User
            {
                Id = AdminUserId,
                Email = "admin@example.com",
                PasswordHash = "not-a-real-hash",
                Role = UserRole.Admin,
                FullName = "Admin",
                PhoneNumber = "0000000000",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

        ctx.SaveChanges();
    }

    private static Organization NewOrg(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        Type = OrganizationType.BloodBank,
        Address = "1 Test Rd",
        PhoneNumber = "000",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
