using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpPost]
    [Authorize(Roles = "Donor")]
    public async Task<ActionResult<AppointmentResponseDto>> Create(CreateAppointmentDto dto)
    {
        var donorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _appointmentService.CreateAsync(donorId, dto);

        return Ok(result);
    }
    [HttpGet("{id}")]
public async Task<ActionResult<AppointmentResponseDto>> GetById(Guid id)
{
    var appointment = await _appointmentService.GetByIdAsync(id);

    if (appointment is null)
    {
        return NotFound();
    }

    var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

    var isOwner = appointment.DonorId == currentUserId;
    var isStaffOrAdmin = currentUserRole is "Staff" or "Admin";

    if (!isOwner && !isStaffOrAdmin)
    {
        return Forbid();
    }

    return Ok(appointment);
}
[HttpPut("{id}/status")]
public async Task<IActionResult> UpdateStatus(Guid id, UpdateAppointmentStatusDto dto)
{
    var appointment = await _appointmentService.GetByIdAsync(id);

    if (appointment is null)
    {
        return NotFound();
    }

    var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

    var isOwner = appointment.DonorId == currentUserId;
    var isStaffOrAdmin = currentUserRole is "Staff" or "Admin";

    if (!isOwner && !isStaffOrAdmin)
    {
        return Forbid();
    }

    var validStatuses = new[] { "scheduled", "completed", "no_show", "cancelled" };
    if (!validStatuses.Contains(dto.NewStatus))
    {
        return BadRequest(new { error = $"Invalid appointment status: '{dto.NewStatus}'" });
    }

    // Donors can only cancel their own appointment — not mark completed/no_show
    if (isOwner && !isStaffOrAdmin && dto.NewStatus != "cancelled")
    {
        return BadRequest(new { error = "Donors may only cancel their own appointments." });
    }

    var result = await _appointmentService.UpdateStatusAsync(id, dto);
    return Ok(result);
}

[HttpGet("donor/{donorId}")]
public async Task<ActionResult<List<AppointmentResponseDto>>> GetByDonor(Guid donorId)
{
    var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

    var isSelf = donorId == currentUserId;
    var isAdmin = currentUserRole == "Admin";

    if (!isSelf && !isAdmin)
    {
        return Forbid();
    }

    var appointments = await _appointmentService.GetByDonorAsync(donorId);
    return Ok(appointments);
}

[HttpGet("bloodbank/{orgId}/upcoming")]
[Authorize(Roles = "Staff,Admin")]
public async Task<ActionResult<PagedResultDto<AppointmentResponseDto>>> GetUpcomingByOrganization(
    Guid orgId, int page = 1, int pageSize = 20)
{
    var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
    var isAdmin = currentUserRole == "Admin";

    try
    {
        var result = await _appointmentService.GetUpcomingByOrganizationAsync(
            orgId, page, pageSize, currentUserId, isAdmin);
        return Ok(result);
    }
    catch (UnauthorizedAccessException)
    {
        return Forbid();
    }
}

}