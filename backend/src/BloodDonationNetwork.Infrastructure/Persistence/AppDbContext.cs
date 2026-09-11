using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<DonorProfile> DonorProfiles => Set<DonorProfile>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<User> Users => Set<User>();

    public DbSet<DonationAppointment> DonationAppointments => Set<DonationAppointment>();

    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();

    public DbSet<BloodBankInventory> BloodBankInventories => Set<BloodBankInventory>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<DonorDevice> DonorDevices => Set<DonorDevice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);

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

            entity.HasIndex(x => new
            {
                x.OrganizationId,
                x.BloodType
            })
            .IsUnique();
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