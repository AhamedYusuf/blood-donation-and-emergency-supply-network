namespace BloodDonationNetwork.Domain.Entities;

/// <summary>
/// An admin-issued, expiring invitation that lets exactly one person
/// create a staff account at a specific organization. This is the ONLY
/// path that can produce a non-donor account — public self-registration
/// (<c>POST /api/auth/register</c>) always creates a donor, ignoring
/// whatever role/organization it's asked for (see AuthService).
/// </summary>
public class StaffInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Who the invite is for. Case-insensitively matched against
    /// the email the invitee registers with.</summary>
    public string Email { get; set; } = default!;

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>The admin who issued this invite.</summary>
    public Guid InvitedByUserId { get; set; }

    /// <summary>Opaque, high-entropy, URL-safe — shared with the invitee
    /// out of band (there's no email infrastructure in this project, so
    /// the admin copies this from the create-invitation response).</summary>
    public string Token { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Null until the invite is redeemed. Once set, the token can
    /// never be used again — invitations are single-use.</summary>
    public DateTime? AcceptedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsUsable => AcceptedAt is null && ExpiresAt > DateTime.UtcNow;
}
