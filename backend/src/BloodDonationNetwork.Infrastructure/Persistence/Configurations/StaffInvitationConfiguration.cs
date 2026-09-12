using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BloodDonationNetwork.Infrastructure.Persistence.Configurations;

public class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> b)
    {
        b.HasKey(i => i.Id);

        b.Property(i => i.Email).HasMaxLength(256).IsRequired();
        b.Property(i => i.Token).HasMaxLength(64).IsRequired();

        // A token is the whole security model here — must be unique and
        // fast to look up on accept.
        b.HasIndex(i => i.Token).IsUnique();
        b.HasIndex(i => i.Email);

        b.HasOne(i => i.Organization)
            .WithMany()
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // The inviting admin — a real FK (unlike DonationAppointments/
        // DonorDevices' plain-Guid links), since this table is new and
        // there's no legacy data to worry about.
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
