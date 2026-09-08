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
        return first is null ? null : (double.Parse(first.lat), double.Parse(first.lon));
    }
    private record NominatimResult(string lat, string lon);
}