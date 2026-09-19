namespace BloodDonationNetwork.Application.DTOs.Agents;

// Shape matches Tech Doc §4.4 Mode 1 output exactly: donorId, distanceKm,
// reliabilityScore, rank (ordinal position in the ranked list, 1-based).
public class DonorCandidateDto
{
    public Guid DonorId { get; set; }
    public double DistanceKm { get; set; }
    public decimal ReliabilityScore { get; set; }
    public int Rank { get; set; }
}
