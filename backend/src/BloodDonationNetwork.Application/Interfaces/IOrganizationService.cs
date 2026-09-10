using BloodDonationNetwork.Application.DTOs.Organizations;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IOrganizationService
{
    Task<IReadOnlyList<OrganizationResponse>> GetAllAsync(
        CancellationToken ct);

    Task<OrganizationResponse?> GetByIdAsync(
        Guid id,
        CancellationToken ct);

    Task<OrganizationResponse> CreateAsync(
        CreateOrganizationRequest request,
        CancellationToken ct);

    Task<OrganizationResponse> UpdateAsync(
        Guid id,
        UpdateOrganizationRequest request,
        CancellationToken ct);

    Task DeleteAsync(
        Guid id,
        CancellationToken ct);
}
