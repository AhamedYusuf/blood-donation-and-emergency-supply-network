using System.Text.Json;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.TestSupport;

/// <summary>
/// AppDbContext subclass for SQLite-backed integration tests.
///
/// (1) Maps <see cref="DonorProfile.MedicalFlags"/> with a plain
///     JSON&lt;-&gt;string converter, because the production mapping relies on
///     the Npgsql <c>jsonb</c> provider type which SQLite does not have.
/// (2) Can be told to throw on the Nth <see cref="SaveChangesAsync"/> call
///     via <see cref="FailOnSaveNumber"/>, to simulate a mid-transaction
///     failure.
/// </summary>
public sealed class TestAppDbContext : AppDbContext
{
    public int FailOnSaveNumber { get; init; } = int.MaxValue;
    private int _saveCount;

    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DonorProfile>()
            .Property(d => d.MedicalFlags)
            .HasColumnType("TEXT")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, bool>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, bool>());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _saveCount++;
        if (_saveCount == FailOnSaveNumber)
        {
            throw new InvalidOperationException(
                $"Forced failure on SaveChangesAsync call #{_saveCount} (rollback test).");
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
