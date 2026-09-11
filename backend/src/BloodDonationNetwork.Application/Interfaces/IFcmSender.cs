namespace BloodDonationNetwork.Application.Interfaces;

/// <summary>Outcome of a single push to one device token.</summary>
public enum FcmSendOutcome
{
    /// <summary>FCM accepted the message.</summary>
    Delivered,

    /// <summary>The token is unregistered / malformed — the caller should
    /// drop it from storage.</summary>
    InvalidToken,

    /// <summary>Timeout, 5xx, quota/rate-limit or network error — worth a
    /// retry.</summary>
    TransientFailure,

    /// <summary>FCM credentials are not configured on this environment; no
    /// call was attempted.</summary>
    NotConfigured
}

/// <summary>
/// The thin seam over Firebase Cloud Messaging. The real implementation
/// talks to the FCM HTTP v1 API; tests substitute a fake. It must never
/// throw for a delivery problem — it returns an <see cref="FcmSendOutcome"/>.
/// </summary>
public interface IFcmSender
{
    Task<FcmSendOutcome> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
