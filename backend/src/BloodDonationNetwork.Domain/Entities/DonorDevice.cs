namespace BloodDonationNetwork.Domain.Entities;

/// <summary>
/// A donor's registered device for push notifications. One donor may have
/// several (phone, tablet). Keyed for lookup by <see cref="DonorUserId"/>
/// (the donor's <c>User.Id</c>, matching the DonationAppointment convention);
/// no FK for now, consistent with the other cross-component links.
/// </summary>
public class DonorDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DonorUserId { get; set; }

    /// <summary>The FCM registration token. Unique — re-registering the same
    /// token just refreshes <see cref="LastSeenAt"/>.</summary>
    public string FcmToken { get; set; } = default!;

    /// <summary>"android" | "ios" | "web".</summary>
    public string Platform { get; set; } = "android";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
