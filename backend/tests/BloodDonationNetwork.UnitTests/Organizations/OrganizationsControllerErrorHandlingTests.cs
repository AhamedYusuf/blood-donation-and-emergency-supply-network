using BloodDonationNetwork.Api.Controllers;
using BloodDonationNetwork.Application.DTOs.Organizations;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.UnitTests.Organizations;

// Create/Update only caught InvalidOperationException, but
// OrganizationService.ParseOrganizationType (and GeocodeAddressAsync) throw
// ArgumentException for ordinary bad input (an invalid Type string, a blank
// address) — that fell through to the global handler's generic 500 instead
// of a proper 400. The Geocode action already caught both correctly; this
// was a gap on Create/Update specifically.
public class OrganizationsControllerErrorHandlingTests
{
    [Fact]
    public async Task Create_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var controller = new OrganizationsController(
            new ThrowingOrganizationService(new ArgumentException("Unsupported organization type: 'clinic'.")));

        var result = await controller.Create(new CreateOrganizationRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var controller = new OrganizationsController(
            new ThrowingOrganizationService(new ArgumentException("Unsupported organization type: 'clinic'.")));

        var result = await controller.Update(Guid.NewGuid(), new UpdateOrganizationRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    private sealed class ThrowingOrganizationService(Exception exception) : IOrganizationService
    {
        public Task<IReadOnlyList<OrganizationResponse>> GetAllAsync(CancellationToken ct) =>
            throw exception;

        public Task<OrganizationResponse?> GetByIdAsync(Guid id, CancellationToken ct) =>
            throw exception;

        public Task<(double Latitude, double Longitude)> GeocodeAddressAsync(string address, CancellationToken ct) =>
            throw exception;

        public Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken ct) =>
            throw exception;

        public Task<OrganizationResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken ct) =>
            throw exception;

        public Task DeleteAsync(Guid id, CancellationToken ct) =>
            throw exception;
    }
}
