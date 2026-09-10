using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Donors;
using BloodDonationNetwork.Application.Interfaces;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/donors")]
[Authorize]
public class DonorsController : ControllerBase
{
    private readonly IDonorService _donorService;

    public DonorsController(IDonorService donorService)
    {
        _donorService = donorService;
    }

    [HttpPost("register")]
    [Authorize(Roles = "donor")]
    public async Task<ActionResult<DonorProfileResponse>> Register(
        [FromBody] DonorRegisterRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            var result = await _donorService.RegisterAsync(userId, request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DonorProfileResponse>> GetById(Guid id, CancellationToken ct)
    {
        if (!await CanAccessDonorAsync(id, ct, allowAdmin: true))
            return Forbid();

        var result = await _donorService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DonorProfileResponse>> Update(
        Guid id, [FromBody] DonorUpdateRequest request, CancellationToken ct)
    {
        if (!await CanAccessDonorAsync(id, ct, allowAdmin: true))
            return Forbid();

        var result = await _donorService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Roles = "staff,admin")]
    public async Task<ActionResult<PagedResult<DonorProfileResponse>>> Search(
        [FromQuery] string bloodType,
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] double? radiusKm,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _donorService.SearchAsync(bloodType, lat, lng, radiusKm, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        await _donorService.VerifyAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/eligibility")]
    public async Task<ActionResult<EligibilityResponse>> Eligibility(Guid id, CancellationToken ct)
    {
        if (!await CanAccessDonorAsync(id, ct, allowAdmin: false))
            return Forbid();

        var result = await _donorService.GetEligibilityAsync(id, ct);
        return Ok(result);
    }

    private async Task<bool> CanAccessDonorAsync(Guid donorId, CancellationToken ct, bool allowAdmin)
    {
        if (User.IsInRole("staff") || (allowAdmin && User.IsInRole("admin")))
            return true;

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return false;

        var donor = await _donorService.GetByIdAsync(donorId, ct);
        return donor?.UserId == userId;
    }
}