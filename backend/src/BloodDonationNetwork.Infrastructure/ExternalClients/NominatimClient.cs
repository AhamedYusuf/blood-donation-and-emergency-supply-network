using System.Globalization;
using System.Net.Http.Json;
using BloodDonationNetwork.Application.Interfaces;

namespace BloodDonationNetwork.Infrastructure.ExternalClients;

public class NominatimClient : IGeocodingClient
{
    private readonly HttpClient _http;
    public NominatimClient(HttpClient http) => _http = http;

    public async Task<(double, double)?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(address)}&format=json&limit=1";

        List<NominatimResult>? resp;

        try
        {
            resp = await _http.GetFromJsonAsync<List<NominatimResult>>(url, ct);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
                  TaskCanceledException or
                  System.Text.Json.JsonException)
        {
            // Nominatim is a free, shared service that actively rate-limits
            // and sometimes blocks requests from cloud/datacenter IP ranges
            // (exactly what a Render-hosted backend is) — a non-success
            // response, a timeout, or an unexpected body here previously
            // threw straight through as an unhandled exception, turning
            // donor registration into a raw 500 instead of the same
            // graceful "could not locate that address" 400 a genuinely
            // unmatched address already produces below. Treating any of
            // these as "couldn't geocode" keeps the caller's contract the
            // same either way.
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
    private record NominatimResult(string lat, string lon);
}