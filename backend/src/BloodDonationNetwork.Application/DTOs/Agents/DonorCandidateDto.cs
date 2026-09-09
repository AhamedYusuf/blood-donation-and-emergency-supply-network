namespace BloodDonationNetwork.Application.DTOs.Agents;

public class DonorCandidateDto
{
    public Guid DonorId { get; set; }
    public double DistanceKm { get; set; }
    public double Score { get; set; }
    public decimal ReliabilityScore { get; set; }
}