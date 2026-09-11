using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodDonationNetwork.Infrastructure.Persistence.Configurations;

public class DonorDeviceConfiguration : IEntityTypeConfiguration<DonorDevice>
{
    public void Configure(EntityTypeBuilder<DonorDevice> b)
    {
        b.HasKey(d => d.Id);

        // One row per FCM token — re-registration is an upsert.
        b.HasIndex(d => d.FcmToken).IsUnique();
        b.HasIndex(d => d.DonorUserId);

        b.Property(d => d.FcmToken).HasMaxLength(4096).IsRequired();
        b.Property(d => d.Platform).HasMaxLength(16).IsRequired();
    }
}
