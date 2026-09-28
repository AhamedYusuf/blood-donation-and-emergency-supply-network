using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Donors;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IDonorService
{
    Task<DonorProfileResponse> RegisterAsync(Guid userId, DonorRegisterRequest request, CancellationToken ct);
    Task<DonorProfileResponse?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<DonorProfileResponse> UpdateAsync(Guid id, DonorUpdateRequest request, CancellationToken ct);
    Task<DonorProfileResponse> UpdateMedicalFlagsAsync(Guid id, Dictionary<string, bool> medicalFlags, CancellationToken ct);
    Task<PagedResult<DonorProfileResponse>> SearchAsync(string? bloodType, string? search, bool eligibleOnly, int page, int pageSize, CancellationToken ct);
    Task VerifyAsync(Guid id, CancellationToken ct);
    Task<EligibilityResponse> GetEligibilityAsync(Guid id, CancellationToken ct);
}