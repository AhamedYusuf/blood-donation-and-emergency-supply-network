namespace BloodDonationNetwork.Infrastructure.ExternalClients.Fcm;

/// <summary>
/// Bound from the <c>Fcm</c> configuration section. Supply either a path to
/// the Firebase service-account JSON, or the JSON itself (e.g. from an
/// environment variable in a deployed environment). If neither is set the
/// app still runs — a no-op sender is used and pushes are skipped.
/// </summary>
public sealed class FcmOptions
{
    /// <summary>Filesystem path to <c>firebase-service-account.json</c>.</summary>
    public string? ServiceAccountKeyPath { get; set; }

    /// <summary>The service-account JSON inline (alternative to the path).</summary>
    public string? ServiceAccountKeyJson { get; set; }

    /// <summary>Optional override; otherwise taken from the key's
    /// <c>project_id</c>.</summary>
    public string? ProjectId { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 10;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(ServiceAccountKeyPath) ||
        !string.IsNullOrWhiteSpace(ServiceAccountKeyJson);
}
