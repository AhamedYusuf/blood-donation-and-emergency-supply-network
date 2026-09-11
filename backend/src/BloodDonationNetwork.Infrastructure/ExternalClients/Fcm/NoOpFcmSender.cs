using BloodDonationNetwork.Application.Interfaces;

namespace BloodDonationNetwork.Infrastructure.ExternalClients.Fcm;

/// <summary>
/// Used when no Firebase credentials are configured, so the API still runs
/// end-to-end in development. Every send reports <see cref="FcmSendOutcome.NotConfigured"/>.
/// </summary>
public sealed class NoOpFcmSender : IFcmSender
{
    public Task<FcmSendOutcome> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(FcmSendOutcome.NotConfigured);
}
