using BloodDonationNetwork.Application.DTOs.Agents;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers.Internal;

[ApiController]
[Route("api/internal/agent")]
public class MatchingDispatchAgentController : ControllerBase
{
    private readonly IMatchingDispatchAgentService _agentService;

    public MatchingDispatchAgentController(IMatchingDispatchAgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpPost("search-donors")]
    public async Task<ActionResult<SearchDonorsResponseDto>> SearchDonors(SearchDonorsRequestDto request)
    {
        var result = await _agentService.SearchDonorsAsync(request);
        return Ok(result);
    }

    // Mode 2 per §4.4 — the server-side guard (reject unless the
    // workflow is "approved") lives in MatchingDispatchAgentService,
    // ahead of any dispatch logic, per the Tech Doc's explicit security
    // note. AgentWorkflow landed via Student 2's PR, unblocking this.
    [HttpPost("dispatch")]
    public async Task<ActionResult<DispatchResponseDto>> Dispatch(DispatchRequestDto request)
    {
        try
        {
            var result = await _agentService.DispatchAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}