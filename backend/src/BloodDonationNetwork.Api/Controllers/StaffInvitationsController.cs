using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Auth;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

/// <summary>Admin-only management of staff invitations — the sole path to
/// creating a staff account (see AuthController.RegisterStaff for the
/// public accept side).</summary>
[ApiController]
[Route("api/staff-invitations")]
[Authorize(Roles = "admin")]
public class StaffInvitationsController : ControllerBase
{
    private readonly IStaffInvitationService _staffInvitationService;

    public StaffInvitationsController(IStaffInvitationService staffInvitationService)
    {
        _staffInvitationService = staffInvitationService;
    }

    [HttpPost]
    public async Task<ActionResult<StaffInvitationResponse>> Create(CreateStaffInvitationRequest request)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var result = await _staffInvitationService.CreateAsync(adminId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffInvitationResponse>>> GetByOrganization(
        [FromQuery] Guid organizationId)
    {
        try
        {
            return Ok(await _staffInvitationService.GetByOrganizationAsync(organizationId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
