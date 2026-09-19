using BloodDonationNetwork.Application.DTOs.Organizations;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationsController(
        IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationResponse>>> GetAll(
        CancellationToken ct)
    {
        var organizations =
            await _organizationService.GetAllAsync(ct);

        return Ok(organizations);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var organization =
            await _organizationService.GetByIdAsync(id, ct);

        if (organization == null)
        {
            return NotFound(new
            {
                message = "Organization not found."
            });
        }

        return Ok(organization);
    }

    [HttpPost("geocode")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Geocode(
        [FromBody] GeocodeOrganizationRequest request,
        CancellationToken ct)
    {
        try
        {
            var coordinates = await _organizationService.GeocodeAddressAsync(
                request.Address,
                ct);

            return Ok(new
            {
                latitude = coordinates.Latitude,
                longitude = coordinates.Longitude
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<OrganizationResponse>> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken ct)
    {
        try
        {
            var organization =
                await _organizationService.CreateAsync(request, ct);

            return CreatedAtAction(
                nameof(GetById),
                new { id = organization.Id },
                organization);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<OrganizationResponse>> Update(
        Guid id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken ct)
    {
        try
        {
            var organization =
                await _organizationService.UpdateAsync(id, request, ct);

            return Ok(organization);
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct)
    {
        await _organizationService.DeleteAsync(id, ct);

        return NoContent();
    }
}
