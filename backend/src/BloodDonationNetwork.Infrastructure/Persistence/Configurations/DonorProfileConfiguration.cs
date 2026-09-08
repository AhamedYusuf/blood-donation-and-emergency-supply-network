using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodDonationNetwork.Infrastructure.Persistence.Configurations;

public class DonorProfileConfiguration : IEntityTypeConfiguration<DonorProfile>
{
    public void Configure(EntityTypeBuilder<DonorProfile> b)
    {
        b.HasIndex(d => d.UserId).IsUnique();
        b.Property(d => d.BloodType).HasMaxLength(3);
        b.Property(d => d.MedicalFlags).HasColumnType("jsonb");
        b.Property(d => d.ReliabilityScore).HasColumnType("numeric(3,2)");
    }
}