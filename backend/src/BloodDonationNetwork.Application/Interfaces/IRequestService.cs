using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IRequestService
{
    Task<RequestResponseDto> CreateAsync(CreateRequestDto dto);

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

    Task<RequestResponseDto?> UpdateStatusAsync(
        Guid id,
        RequestStatusUpdateDto dto);

    Task<bool> DeleteAsync(Guid id);

    Task<RequestResponseDto?> CloseAsync(Guid id);
}