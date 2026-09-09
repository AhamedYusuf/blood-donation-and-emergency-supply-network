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

    // TODO (blocked — waiting on Student 2's AgentWorkflow table):
    // POST /api/internal/agent/dispatch per §4.4 mode 2. Must implement the
    // server-side guard FIRST (reject if agent_workflows.status != approved)
    // before any dispatch logic, per Tech Doc's explicit security note.
}