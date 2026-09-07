namespace BloodDonationNetwork.Application.Interfaces;

public interface IGeocodingClient
{
    Task<(double Lat, double Lng)?> GeocodeAsync(string address, CancellationToken ct = default);
}