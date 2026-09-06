using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    // DbSet properties — EF snake_case convention (set in Program.cs) maps
    // C# PascalCase properties to snake_case DB columns automatically.
    public DbSet<DonorProfile> DonorProfiles => Set<DonorProfile>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}