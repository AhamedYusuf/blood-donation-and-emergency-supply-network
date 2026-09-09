namespace BloodDonationNetwork.Application.DTOs.Agents;

public class SearchDonorsResponseDto
{
    public List<DonorCandidateDto> Candidates { get; set; } = new();
}
