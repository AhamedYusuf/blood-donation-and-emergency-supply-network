using System.Net.Http.Headers;
using System.Net.Http.Json;
using BloodDonationNetwork.Application.Interfaces;

namespace BloodDonationNetwork.Infrastructure.ExternalClients.Fcm;

/// <summary>
/// <see cref="IFcmSender"/> backed by the FCM HTTP v1 API. Maps HTTP
/// outcomes to <see cref="FcmSendOutcome"/> and enforces a per-call
/// timeout. Never throws for a delivery failure.
/// </summary>
public sealed class FcmHttpSender : IFcmSender
{
    private readonly HttpClient _http;
    private readonly GoogleAccessTokenProvider _tokens;
    private readonly string _projectId;
    private readonly TimeSpan _timeout;

    public FcmHttpSender(
        HttpClient http,
        GoogleAccessTokenProvider tokens,
        string projectId,
        TimeSpan timeout)
    {
        _http = http;
        _tokens = tokens;
        _projectId = projectId;
        _timeout = timeout;
    }

    public async Task<FcmSendOutcome> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            var accessToken = await _tokens.GetAsync(cts.Token);

            var message = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new { title, body },
                    data = data ?? new Dictionary<string, string>(),
                },
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send")
            {
                Content = JsonContent.Create(message),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return FcmSendOutcome.Delivered;
            }

            var payload = await response.Content.ReadAsStringAsync(cts.Token);
            var status = (int)response.StatusCode;

            if (status is 400 or 404 && IndicatesDeadToken(payload))
            {
                return FcmSendOutcome.InvalidToken;
            }

            // 401/403 (token/permission), 429 (quota), 5xx, other 4xx — retryable.
            return FcmSendOutcome.TransientFailure;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own timeout elapsed, not a caller cancellation.
            return FcmSendOutcome.TransientFailure;
        }
        catch (HttpRequestException)
        {
            return FcmSendOutcome.TransientFailure;
        }
        catch (InvalidOperationException)
        {
            // Token exchange failed (bad credentials, network) — retryable.
            return FcmSendOutcome.TransientFailure;
        }
    }

    private static bool IndicatesDeadToken(string payload) =>
        payload.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase) ||
        payload.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase) ||
        payload.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase);
}
