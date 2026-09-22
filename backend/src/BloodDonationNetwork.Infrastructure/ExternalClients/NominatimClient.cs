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
        var resp = await _http.GetFromJsonAsync<List<NominatimResult>>(url, ct);
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