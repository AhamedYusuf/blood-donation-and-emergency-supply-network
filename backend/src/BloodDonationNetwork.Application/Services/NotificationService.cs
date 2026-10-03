using System.Text.Json;
using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Notifications;
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
        // Written unconditionally, before the push attempt — the inbox is
        // the reliable record of "this donor was notified," independent
        // of whether a push could actually be delivered (no registered
        // devices, FCM not configured, every device transiently failing
        // are all real possibilities this method already tolerates).
        // Every return path below falls through to one SaveChangesAsync
        // at the end so this row is never silently dropped on an
        // early-return branch.
        _db.DonorNotifications.Add(new DonorNotification
        {
            Id = Guid.NewGuid(),
            DonorUserId = donorUserId,
            Title = title,
            Body = body,
            DataJson = data is { Count: > 0 }
                ? JsonSerializer.Serialize(data)
                : null,
        });

        var devices = await _db.DonorDevices
            .Where(d => d.DonorUserId == donorUserId)
            .ToListAsync(cancellationToken);

        NotificationResult result;

        if (devices.Count == 0)
        {
            result = NotificationResult.NoDevices(donorUserId);
        }
        else
        {
            var delivered = 0;
            var deadTokens = new List<DonorDevice>();
            var notConfigured = false;

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
                        notConfigured = true;
                        break;

                    case FcmSendOutcome.TransientFailure:
                        // Exhausted retries for this device; recorded in
                        // the aggregate result below.
                        break;
                }

                if (notConfigured)
                {
                    break;
                }
            }

            if (deadTokens.Count > 0)
            {
                _db.DonorDevices.RemoveRange(deadTokens);
            }

            result = notConfigured
                ? NotificationResult.NotConfigured(donorUserId)
                : new NotificationResult(
                    DonorUserId: donorUserId,
                    DevicesTried: devices.Count,
                    Delivered: delivered,
                    InvalidTokensPruned: deadTokens.Count,
                    AnyDelivered: delivered > 0,
                    FailureReason: delivered == 0 ? "No device accepted the notification" : null);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return result;
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

    public async Task<PagedResult<DonorNotificationDto>> GetForDonorAsync(
        Guid donorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.DonorNotifications
            .AsNoTracking()
            .Where(n => n.DonorUserId == donorUserId)
            .OrderByDescending(n => n.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<DonorNotificationDto>
        {
            Items = items.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public Task<int> GetUnreadCountAsync(
        Guid donorUserId,
        CancellationToken cancellationToken = default) =>
        _db.DonorNotifications
            .Where(n => n.DonorUserId == donorUserId && n.ReadAt == null)
            .CountAsync(cancellationToken);

    public async Task MarkAsReadAsync(
        Guid id,
        Guid donorUserId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.DonorNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Notification {id} not found.");

        if (notification.DonorUserId != donorUserId)
        {
            throw new UnauthorizedAccessException(
                "You may only mark your own notifications as read.");
        }

        // Idempotent — marking an already-read notification again just
        // keeps its original ReadAt rather than bumping it forward.
        notification.ReadAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static DonorNotificationDto ToDto(DonorNotification n) => new(
        n.Id,
        n.Title,
        n.Body,
        n.DataJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(n.DataJson),
        n.CreatedAt,
        n.ReadAt);

    private static string NormalisePlatform(string platform)
    {
        var p = (platform ?? "").Trim().ToLowerInvariant();
        return p is "android" or "ios" or "web" ? p : "android";
    }
}
