using Microsoft.EntityFrameworkCore;
using BloodDonationNetwork.Domain.Entities;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<DonorProfile> DonorProfiles { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}