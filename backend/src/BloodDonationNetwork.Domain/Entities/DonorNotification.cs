namespace BloodDonationNetwork.Domain.Entities;

/// <summary>
/// A durable record of a notification sent (or attempted) to a donor —
/// the in-app inbox's backing row. Written by NotificationService
/// alongside the push attempt itself, regardless of whether the push
/// actually delivered (no devices registered, FCM not configured, a
/// transient failure) — the inbox is the reliable record; the push is
/// just a best-effort nudge to go look at it. Keyed by DonorUserId
/// (User.Id), matching every other donor-facing table's convention; no
/// FK, consistent with the other cross-component links in this codebase.
/// </summary>
public class DonorNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DonorUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Optional structured payload (e.g. {"appointmentId": "..."})
    /// serialized as JSON, so the app can deep-link on tap. Null when a
    /// notification carries no actionable data.</summary>
    public string? DataJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null until the donor opens/reads it in the inbox.</summary>
    public DateTime? ReadAt { get; set; }
}
