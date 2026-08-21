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
}