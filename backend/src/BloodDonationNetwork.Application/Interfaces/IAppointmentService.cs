using BloodDonationNetwork.Application.DTOs.Appointments;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentResponseDto> CreateAsync(Guid donorId, CreateAppointmentDto dto);
    Task<AppointmentResponseDto?> GetByIdAsync(Guid id);
    Task<AppointmentResponseDto> UpdateStatusAsync(Guid id, UpdateAppointmentStatusDto dto);
    Task<List<AppointmentResponseDto>> GetByDonorAsync(Guid donorId);
    Task<PagedResultDto<AppointmentResponseDto>> GetUpcomingByOrganizationAsync(
        Guid organizationId, int page, int pageSize, Guid requestingUserId, bool isAdmin);
    Task<AppointmentResponseDto> CompleteAsync(Guid id, CompleteAppointmentDto dto);
}