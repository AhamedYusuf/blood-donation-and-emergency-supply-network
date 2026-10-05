using System.Net;

namespace BloodDonationNetwork.Api.Http;

// On Render's free plan the agent service sleeps when idle, and a request from
// the API to a sleeping service gets Render's own 502 page straight away instead
// of being held while it wakes. That page never reaches the agent, so it is safe
// to wait for /health and send the same request once more.
public class AgentServiceWakeUpHandler : DelegatingHandler
{
    private readonly TimeSpan _wakeUpLimit;
    private readonly TimeSpan _pollInterval;

    public AgentServiceWakeUpHandler()
        : this(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(3))
    {
    }

    public AgentServiceWakeUpHandler(TimeSpan wakeUpLimit, TimeSpan pollInterval)
    {
        _wakeUpLimit = wakeUpLimit;
        _pollInterval = pollInterval;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Buffer the body first: a request message can only be sent once,
        // so the retry needs its own copy.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        var response = await base.SendAsync(Copy(request, body), cancellationToken);
        if (!IsGatewayError(response.StatusCode))
        {
            return response;
        }

        if (!await WaitUntilHealthyAsync(request.RequestUri!, cancellationToken))
        {
            return response;
        }

        response.Dispose();
        return await base.SendAsync(Copy(request, body), cancellationToken);
    }

    private async Task<bool> WaitUntilHealthyAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        var healthUri = new Uri(requestUri, "/health");
        var deadline = DateTime.UtcNow + _wakeUpLimit;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var health = await base.SendAsync(
                    new HttpRequestMessage(HttpMethod.Get, healthUri), cancellationToken);
                if (health.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        return false;
    }

    private static bool IsGatewayError(HttpStatusCode status) =>
        status is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static HttpRequestMessage Copy(HttpRequestMessage original, byte[]? body)
    {
        var copy = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };

        foreach (var header in original.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null)
        {
            copy.Content = new ByteArrayContent(body);
            foreach (var header in original.Content!.Headers)
            {
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return copy;
    }
}
