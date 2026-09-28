using System.Globalization;
using System.Net.Http.Json;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BloodDonationNetwork.Infrastructure.ExternalClients;

// Replaces NominatimClient. Nominatim's free public instance actively
// rate-limits and blocks requests from cloud/datacenter IP ranges —
// confirmed live: donor registration and organization creation both
// reliably failed to geocode once deployed to Render, for addresses
// verified to resolve fine from a residential network. LocationIQ is
// built on the same OpenStreetMap data and exposes a (deliberately)
// Nominatim-compatible response shape, but as a hosted, paid-infra
// service that treats normal app traffic as legitimate rather than
// abuse — free tier covers this project's needs with room to spare.
public class LocationIqClient : IGeocodingClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public LocationIqClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;

        _apiKey =
            configuration["LocationIq:ApiKey"]
            ?? throw new InvalidOperationException(
                "LocationIq:ApiKey (env var LocationIq__ApiKey) is not configured.");
    }

    public async Task<(double Lat, double Lng)?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        var url =
            "https://us1.locationiq.com/v1/search" +
            $"?key={Uri.EscapeDataString(_apiKey)}" +
            $"&q={Uri.EscapeDataString(address)}" +
            "&format=json&limit=1";

        List<LocationIqResult>? resp;

        try
        {
            resp = await _http.GetFromJsonAsync<List<LocationIqResult>>(url, ct);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
                  TaskCanceledException or
                  System.Text.Json.JsonException)
        {
            // A transient failure (rate limit, timeout, bad response) is
            // indistinguishable from "no match" to the caller — both mean
            // no coordinates are available right now, handled the same
            // graceful way as a genuinely unmatched address below.
            return null;
        }

        var first = resp?.FirstOrDefault();
        if (first is null)
            return null;

        if (!double.TryParse(first.lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(first.lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            return null;
        }

        return (lat, lon);
    }

    private record LocationIqResult(string lat, string lon);
}
