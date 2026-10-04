using System.Net.Http.Json;
using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;
    private readonly IAgentWorkflowService _workflowService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        IRequestService requestService,
        IAgentWorkflowService workflowService,
        IHttpClientFactory httpClientFactory,
        ILogger<RequestsController> logger)
    {
        _requestService = requestService;
        _workflowService = workflowService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // POST /api/requests
    [HttpPost]
    [Authorize(Roles = "staff,admin")]
    public async Task<ActionResult<RequestResponseDto>> Create(
        [FromBody] CreateRequestDto dto)
    {
        var (userId, isAdmin) = CurrentUser();

        try
        {
            // 1. Create and save the Blood Request first
            var request = await _requestService.CreateAsync(
                userId,
                userId,
                isAdmin,
                dto);

            // 2. Automatically start the Coordinator Agent workflow
            Uri? agentServiceBaseAddress = null;
            try
            {
                var agentClient =
                    _httpClientFactory.CreateClient("AgentService");
                agentServiceBaseAddress =
                    agentClient.BaseAddress;

                for (var attempt = 1; attempt <= 2; attempt++)
                {
                    var agentResponse =
                        await agentClient.PostAsJsonAsync(
                            "/run-workflow",
                            new
                            {
                                bloodRequestId =
                                    request.Id.ToString()
                            });

                    if (agentResponse.IsSuccessStatusCode)
                    {
                        var createdWorkflow =
                            await _workflowService
                                .GetLatestByBloodRequestIdAsync(
                                    request.Id);

                        if (createdWorkflow is not null)
                        {
                            _logger.LogInformation(
                                "Agent workflow {WorkflowId} started " +
                                "automatically for blood request " +
                                "{BloodRequestId}.",
                                createdWorkflow.Id,
                                request.Id);
                            break;
                        }

                        var successBody =
                            await agentResponse.Content
                                .ReadAsStringAsync();

                        if (attempt == 1)
                        {
                            _logger.LogWarning(
                                "Agent Service returned success for " +
                                "blood request {BloodRequestId}, but no " +
                                "workflow record was found. Retrying once. " +
                                "BaseAddress={BaseAddress}. Response={Response}",
                                request.Id,
                                agentServiceBaseAddress,
                                successBody);
                            continue;
                        }

                        _logger.LogError(
                            "Agent Service returned success for blood " +
                            "request {BloodRequestId}, but no workflow " +
                            "record was persisted after retry. " +
                            "BaseAddress={BaseAddress}. Response={Response}",
                            request.Id,
                            agentServiceBaseAddress,
                            successBody);
                        break;
                    }

                    var errorBody =
                        await agentResponse.Content
                            .ReadAsStringAsync();

                    var existingWorkflow =
                        await _workflowService
                            .GetLatestByBloodRequestIdAsync(
                                request.Id);

                    if (attempt == 1 &&
                        existingWorkflow == null)
                    {
                        _logger.LogWarning(
                            "Agent workflow start failed for blood request " +
                            "{BloodRequestId}; no workflow record exists. " +
                            "Retrying once. Status: {StatusCode}. Response: {Response}",
                            request.Id,
                            agentResponse.StatusCode,
                            errorBody);
                        continue;
                    }

                    _logger.LogWarning(
                        "Blood request {BloodRequestId} was created, " +
                        "but the agent workflow could not be started. " +
                        "WorkflowRecordExists={WorkflowRecordExists}. " +
                        "Status: {StatusCode}. Response: {Response}",
                        request.Id,
                        existingWorkflow != null,
                        agentResponse.StatusCode,
                        errorBody);
                    break;
                }
            }
            catch (Exception ex)
            {
                // Agent failure must not undo a successfully-created
                // Blood Request.
                _logger.LogError(
                    ex,
                    "Blood request {BloodRequestId} was created, " +
                    "but the Agent Service at {BaseAddress} could not " +
                    "complete workflow startup.",
                    agentServiceBaseAddress,
                    request.Id);
            }

            return CreatedAtAction(
                nameof(GetById),
                new { id = request.Id },
                request);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // GET /api/requests/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RequestResponseDto>> GetById(
        Guid id)
    {
        var request =
            await _requestService.GetByIdAsync(id);

        if (request == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        return Ok(request);
    }

    // GET /api/requests
    // nearLat/nearLng/radiusKm: the donor-facing "Blood requests" browse
    // view passes these (their own DonorProfile location) to restrict
    // results to nearby requests instead of every request network-wide.
    // Staff/admin callers omit them and see the unfiltered list as before.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RequestResponseDto>>>
        GetAll(
            [FromQuery] BloodType? bloodType = null,
            [FromQuery] RequestUrgency? urgency = null,
            [FromQuery] string? status = null,
            [FromQuery] Guid? organizationId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "createdAt",
            [FromQuery] bool descending = true,
            [FromQuery] double? nearLat = null,
            [FromQuery] double? nearLng = null,
            [FromQuery] double? radiusKm = null)
    {
        try
        {
            if (!Guid.TryParse(
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var requestingUserId) ||
                !Enum.TryParse<UserRole>(
                    User.FindFirstValue(ClaimTypes.Role),
                    ignoreCase: true,
                    out var requestingUserRole) ||
                !Enum.IsDefined(
                    typeof(UserRole),
                    requestingUserRole))
            {
                return Forbid();
            }

            Guid? donorUserId =
                requestingUserRole == UserRole.Donor
                    ? requestingUserId
                    : null;

            var requests =
                await _requestService.GetAllAsync(
                    requestingUserId,
                    requestingUserRole,
                    bloodType,
                    urgency,
                    status,
                    organizationId,
                    page,
                    pageSize,
                    sortBy,
                    descending,
                    nearLat,
                    nearLng,
                    radiusKm,
                    donorUserId);

            return Ok(requests);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // PUT /api/requests/{id}/status
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "staff,admin")]
    public async Task<ActionResult<RequestResponseDto>> UpdateStatus(
        Guid id,
        [FromBody] RequestStatusUpdateDto dto)
    {
        var (userId, isAdmin) = CurrentUser();

        try
        {
            var request =
                await _requestService.UpdateStatusAsync(
                    id,
                    userId,
                    isAdmin,
                    dto);

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Blood request not found."
                });
            }

            return Ok(request);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // DELETE /api/requests/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (userId, isAdmin) = CurrentUser();

        try
        {
            var deleted =
                await _requestService.DeleteAsync(
                    id,
                    userId,
                    isAdmin);

            if (!deleted)
            {
                return NotFound(new
                {
                    message = "Blood request not found."
                });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // POST /api/requests/{id}/close
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "staff,admin")]
    public async Task<ActionResult<RequestResponseDto>> Close(
        Guid id)
    {
        var (userId, isAdmin) = CurrentUser();

        try
        {
            var request =
                await _requestService.CloseAsync(
                    id,
                    userId,
                    isAdmin);

            if (request == null)
            {
                return NotFound(new
                {
                    message = "Blood request not found."
                });
            }

            return Ok(request);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private (Guid UserId, bool IsAdmin) CurrentUser()
    {
        var userId = Guid.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!);

        var isAdmin =
            User.FindFirstValue(
                ClaimTypes.Role) == UserRoles.Admin;

        return (userId, isAdmin);
    }
}