using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requestService;

    public RequestsController(IRequestService requestService)
    {
        _requestService = requestService;
    }

    // POST /api/requests
    [HttpPost]
    public async Task<ActionResult<RequestResponseDto>> Create(
        [FromBody] CreateRequestDto dto)
    {
        var request = await _requestService.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = request.Id },
            request);
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
    public async Task<ActionResult<RequestResponseDto>> UpdateStatus(
        Guid id,
        [FromBody] RequestStatusUpdateDto dto)
    {
        var request = await _requestService.UpdateStatusAsync(id, dto);

        if (request == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        return Ok(request);
    }

    // DELETE /api/requests/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _requestService.DeleteAsync(id);

        if (!deleted)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        return NoContent();
    }

    // POST /api/requests/{id}/close
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<RequestResponseDto>> Close(Guid id)
    {
        var request = await _requestService.CloseAsync(id);

        if (request == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        return Ok(request);
    }
}