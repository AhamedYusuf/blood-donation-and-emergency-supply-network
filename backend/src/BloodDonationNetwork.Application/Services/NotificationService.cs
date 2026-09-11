using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Services;

/// <summary>
/// Orchestrates push delivery for one donor: looks up their devices, fans
/// out to each token, retries transient failures up to
/// <see cref="MaxAttemptsPerDevice"/> times, prunes tokens FCM reports as
/// dead, and reports an aggregate result. Never throws for a delivery
/// problem (Tech Doc §0.10 — a failed push must not break the dispatch loop).
/// </summary>
public class NotificationService : INotificationService
{
    public const int MaxAttemptsPerDevice = 3;

    private readonly IApplicationDbContext _db;
    private readonly IFcmSender _fcm;

    public NotificationService(IApplicationDbContext db, IFcmSender fcm)
    {
        _db = db;
        _fcm = fcm;
    }

    public async Task<NotificationResult> SendToDonorAsync(
        Guid donorUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var devices = await _db.DonorDevices
            .Where(d => d.DonorUserId == donorUserId)
            .ToListAsync(cancellationToken);

        if (devices.Count == 0)
        {
            return NotificationResult.NoDevices(donorUserId);
        }

        var delivered = 0;
        var deadTokens = new List<DonorDevice>();

        foreach (var device in devices)
        {
            var outcome = await SendWithRetryAsync(device.FcmToken, title, body, data, cancellationToken);

            switch (outcome)
            {
                case FcmSendOutcome.Delivered:
                    delivered++;
                    device.LastSeenAt = DateTime.UtcNow;
                    break;

                case FcmSendOutcome.InvalidToken:
                    deadTokens.Add(device);
                    break;

                case FcmSendOutcome.NotConfigured:
                    return NotificationResult.NotConfigured(donorUserId);

                case FcmSendOutcome.TransientFailure:
                    // Exhausted retries for this device; recorded in the
                    // aggregate result below.
                    break;
            }
        }

        if (deadTokens.Count > 0)
        {
            _db.DonorDevices.RemoveRange(deadTokens);
        }

        if (delivered > 0 || deadTokens.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new NotificationResult(
            DonorUserId: donorUserId,
            DevicesTried: devices.Count,
            Delivered: delivered,
            InvalidTokensPruned: deadTokens.Count,
            AnyDelivered: delivered > 0,
            FailureReason: delivered == 0 ? "No device accepted the notification" : null);
    }

    private async Task<FcmSendOutcome> SendWithRetryAsync(
        string token,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct)
    {
        FcmSendOutcome outcome = FcmSendOutcome.TransientFailure;

        for (var attempt = 1; attempt <= MaxAttemptsPerDevice; attempt++)
        {
            outcome = await _fcm.SendAsync(token, title, body, data, ct);

            if (outcome is FcmSendOutcome.Delivered
                or FcmSendOutcome.InvalidToken
                or FcmSendOutcome.NotConfigured)
            {
                return outcome;
            }
            // TransientFailure — try again.
        }

        return outcome;
    }

    public async Task RegisterDeviceAsync(
        Guid donorUserId,
        string fcmToken,
        string platform,
        CancellationToken cancellationToken = default)
    {
        fcmToken = fcmToken.Trim();
        if (string.IsNullOrEmpty(fcmToken))
        {
            throw new ArgumentException("FCM token is required.", nameof(fcmToken));
        }

        platform = NormalisePlatform(platform);

        var existing = await _db.DonorDevices
            .FirstOrDefaultAsync(d => d.FcmToken == fcmToken, cancellationToken);

        if (existing is null)
        {
            _db.DonorDevices.Add(new DonorDevice
            {
                Id = Guid.NewGuid(),
                DonorUserId = donorUserId,
                FcmToken = fcmToken,
                Platform = platform,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            });
        }
        else
        {
            // Same physical device can be handed to a different donor account.
            existing.DonorUserId = donorUserId;
            existing.Platform = platform;
            existing.LastSeenAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterDeviceAsync(
        Guid donorUserId,
        string fcmToken,
        CancellationToken cancellationToken = default)
    {
        fcmToken = fcmToken.Trim();

        var device = await _db.DonorDevices
            .FirstOrDefaultAsync(
                d => d.FcmToken == fcmToken && d.DonorUserId == donorUserId,
                cancellationToken);

        if (device is not null)
        {
            _db.DonorDevices.Remove(device);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string NormalisePlatform(string platform)
    {
        var p = (platform ?? "").Trim().ToLowerInvariant();
        return p is "android" or "ios" or "web" ? p : "android";
    }
}
