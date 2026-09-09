using BloodDonationNetwork.Application.DTOs.Agents;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IMatchingDispatchAgentService
{
    Task<SearchDonorsResponseDto> SearchDonorsAsync(SearchDonorsRequestDto request);
}