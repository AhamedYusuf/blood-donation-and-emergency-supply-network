namespace BloodDonationNetwork.Application.Interfaces;

/// <summary>Aggregate result of notifying one donor across all their devices.</summary>
public sealed record NotificationResult(
    Guid DonorUserId,
    int DevicesTried,
    int Delivered,
    int InvalidTokensPruned,
    bool AnyDelivered,
    string? FailureReason = null)
{
    public static NotificationResult NoDevices(Guid donorUserId) =>
        new(donorUserId, 0, 0, 0, false, "Donor has no registered devices");

    public static NotificationResult NotConfigured(Guid donorUserId) =>
        new(donorUserId, 0, 0, 0, false, "Push notifications are not configured");
}

/// <summary>
/// Domain-facing push-notification API. Handles per-donor device lookup,
/// fan-out, bounded retry and pruning of dead tokens. A failure to reach
/// one donor never throws — callers (e.g. the dispatch loop) inspect the
/// <see cref="NotificationResult"/> and move on.
/// </summary>
public interface INotificationService
{
    Task<NotificationResult> SendToDonorAsync(
        Guid donorUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    Task RegisterDeviceAsync(
        Guid donorUserId,
        string fcmToken,
        string platform,
        CancellationToken cancellationToken = default);

    Task UnregisterDeviceAsync(
        Guid donorUserId,
        string fcmToken,
        CancellationToken cancellationToken = default);
}
