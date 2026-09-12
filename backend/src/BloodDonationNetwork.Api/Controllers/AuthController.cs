using BloodDonationNetwork.Application.DTOs.Auth;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IStaffInvitationService _staffInvitationService;

    public AuthController(IAuthService authService, IStaffInvitationService staffInvitationService)
    {
        _authService = authService;
        _staffInvitationService = staffInvitationService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request)
    {
        try
        {
            return Ok(await _authService.RegisterAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request)
    {
        try
        {
            return Ok(await _authService.LoginAsync(request));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] string refreshToken)
    {
        var response = await _authService.RefreshTokenAsync(refreshToken);

        return Ok(response);
    }

    // The only way to get a non-donor account: redeem an admin-issued
    // invitation (see StaffInvitationsController for creating one). Role
    // and organization come from the invitation, never from this request.
    [HttpPost("register/staff")]
    public async Task<ActionResult<AuthResponse>> RegisterStaff(
        AcceptStaffInvitationRequest request)
    {
        try
        {
            return Ok(await _staffInvitationService.AcceptAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}