using BloodDonationNetwork.Application.DTOs.Agents;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IMatchingDispatchAgentService
{
    Task<SearchDonorsResponseDto> SearchDonorsAsync(SearchDonorsRequestDto request);

    /// <summary>Mode 2 — post-approval only. Throws
    /// <see cref="KeyNotFoundException"/> if the workflow doesn't exist,
    /// or <see cref="InvalidOperationException"/> if it exists but isn't
    /// in the "approved" state (Tech Doc §4.4's required guard).</summary>
    Task<DispatchResponseDto> DispatchAsync(DispatchRequestDto request);
}