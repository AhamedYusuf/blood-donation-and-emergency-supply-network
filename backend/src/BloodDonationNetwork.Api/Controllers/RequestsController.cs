using System.Net.Http.Json;
using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        IRequestService requestService,
        IHttpClientFactory httpClientFactory,
        ILogger<RequestsController> logger)
    {
        _requestService = requestService;
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
            try
            {
                var agentClient =
                    _httpClientFactory.CreateClient("AgentService");

                var agentResponse =
                    await agentClient.PostAsJsonAsync(
                        "/run-workflow",
                        new
                        {
                            bloodRequestId =
                                request.Id.ToString()
                        });

                if (!agentResponse.IsSuccessStatusCode)
                {
                    var errorBody =
                        await agentResponse.Content
                            .ReadAsStringAsync();

                    _logger.LogWarning(
                        "Blood request {BloodRequestId} was created, " +
                        "but the agent workflow could not be started. " +
                        "Status: {StatusCode}. Response: {Response}",
                        request.Id,
                        agentResponse.StatusCode,
                        errorBody);
                }
                else
                {
                    _logger.LogInformation(
                        "Agent workflow started automatically for " +
                        "blood request {BloodRequestId}.",
                        request.Id);
                }
            }
            catch (Exception ex)
            {
                // Agent failure must not undo a successfully-created
                // Blood Request.
                _logger.LogError(
                    ex,
                    "Blood request {BloodRequestId} was created, " +
                    "but the Agent Service could not be reached.",
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
            [FromQuery] bool descending = true)
    {
        try
        {
            var requests =
                await _requestService.GetAllAsync(
                    bloodType,
                    urgency,
                    status,
                    organizationId,
                    page,
                    pageSize,
                    sortBy,
                    descending);

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