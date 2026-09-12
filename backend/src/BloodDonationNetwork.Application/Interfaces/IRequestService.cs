using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IRequestService
{
    /// <summary>Throws <see cref="UnauthorizedAccessException"/> if the
    /// caller is staff at a different organization than
    /// <paramref name="dto"/>.OrganizationId.</summary>
    Task<RequestResponseDto> CreateAsync(Guid requesterId, Guid requestingUserId, bool isAdmin, CreateRequestDto dto);

    Task<RequestResponseDto?> GetByIdAsync(Guid id);

    Task<IEnumerable<RequestResponseDto>> GetAllAsync(
        BloodType? bloodType = null,
        RequestUrgency? urgency = null,
        BloodRequestStatus? status = null,
        Guid? organizationId = null,
        int page = 1,
        int pageSize = 10,
        string sortBy = "createdAt",
        bool descending = true);

    /// <summary>Throws <see cref="UnauthorizedAccessException"/> if the
    /// caller is staff at a different organization than the request.</summary>
    Task<RequestResponseDto?> UpdateStatusAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin,
        RequestStatusUpdateDto dto);

    Task<bool> DeleteAsync(Guid id, Guid requestingUserId, bool isAdmin);

    Task<RequestResponseDto?> CloseAsync(Guid id, Guid requestingUserId, bool isAdmin);
}