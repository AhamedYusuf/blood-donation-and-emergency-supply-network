using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

// Previously had no [Authorize] anywhere — anonymous callers could
// create/edit/delete/close any organization's blood requests. Reads stay
// open to any authenticated user (donors need to see compatible requests);
// writes are staff/admin, org-scoped in RequestService the same way
// AppointmentsController/AppointmentService scope appointment completion.
[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;

    public RequestsController(IRequestService requestService)
    {
        _requestService = requestService;
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
            var request = await _requestService.CreateAsync(userId, userId, isAdmin, dto);

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
    public async Task<ActionResult<RequestResponseDto>> GetById(Guid id)
    {
        var request = await _requestService.GetByIdAsync(id);

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
    public async Task<ActionResult<IEnumerable<RequestResponseDto>>> GetAll(
        [FromQuery] BloodType? bloodType = null,
        [FromQuery] RequestUrgency? urgency = null,
        [FromQuery] BloodRequestStatus? status = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] bool descending = true)
    {
        var requests = await _requestService.GetAllAsync(
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
            var request = await _requestService.UpdateStatusAsync(id, userId, isAdmin, dto);

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

    // DELETE /api/requests/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (userId, isAdmin) = CurrentUser();

        try
        {
            var deleted = await _requestService.DeleteAsync(id, userId, isAdmin);

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
    public async Task<ActionResult<RequestResponseDto>> Close(Guid id)
    {
        var (userId, isAdmin) = CurrentUser();

        try
        {
            var request = await _requestService.CloseAsync(id, userId, isAdmin);

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
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.FindFirstValue(ClaimTypes.Role) == "admin";
        return (userId, isAdmin);
    }
}
