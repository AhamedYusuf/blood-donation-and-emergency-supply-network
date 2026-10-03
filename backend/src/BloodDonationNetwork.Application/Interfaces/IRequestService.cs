using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IRequestService
{
    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the caller is
    /// staff at a different organization than <paramref name="dto"/>.OrganizationId.
    /// </summary>
    Task<RequestResponseDto> CreateAsync(
        Guid requesterId,
        Guid requestingUserId,
        bool isAdmin,
        CreateRequestDto dto);

    Task<RequestResponseDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// When <paramref name="nearLat"/>/<paramref name="nearLng"/> are both
    /// supplied, results are restricted to requests within
    /// <paramref name="radiusKm"/> (default 50km) of that point — this is
    /// how the donor-facing browse view limits itself to nearby requests
    /// instead of returning every request network-wide.
    /// </summary>
    Task<IEnumerable<RequestResponseDto>> GetAllAsync(
        Guid requestingUserId,
        UserRole requestingUserRole,
        BloodType? bloodType = null,
        RequestUrgency? urgency = null,
        string? status = null,
        Guid? organizationId = null,
        int page = 1,
        int pageSize = 10,
        string sortBy = "createdAt",
        bool descending = true,
        double? nearLat = null,
        double? nearLng = null,
        double? radiusKm = null,
        Guid? donorUserId = null);

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the caller is
    /// staff at a different organization than the request.
    /// </summary>
    Task<RequestResponseDto?> UpdateStatusAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin,
        RequestStatusUpdateDto dto);

    Task<bool> DeleteAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin);

    Task<RequestResponseDto?> CloseAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin);
}