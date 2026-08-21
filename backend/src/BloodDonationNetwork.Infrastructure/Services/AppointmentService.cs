using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;

    public AppointmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AppointmentResponseDto> CreateAsync(Guid donorId, CreateAppointmentDto dto)
    {
        var appointment = new DonationAppointment
        {
            Id = Guid.NewGuid(),
            DonorId = donorId,
            OrganizationId = dto.OrganizationId,
            RelatedWorkflowId = dto.RelatedWorkflowId,
            ScheduledTime = dto.ScheduledTime,
            Status = AppointmentStatus.Scheduled,
            UnitsDonated = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DonationAppointments.Add(appointment);
        await _context.SaveChangesAsync();

        return MapToDto(appointment);
    }

    public async Task<AppointmentResponseDto?> GetByIdAsync(Guid id)
{
    var appointment = await _context.DonationAppointments
        .FirstOrDefaultAsync(a => a.Id == id);

    return appointment is null ? null : MapToDto(appointment);
}

    public Task<AppointmentResponseDto> UpdateStatusAsync(Guid id, UpdateAppointmentStatusDto dto)
        => throw new NotImplementedException();

    public Task<List<AppointmentResponseDto>> GetByDonorAsync(Guid donorId)
        => throw new NotImplementedException();

    public Task<List<AppointmentResponseDto>> GetUpcomingByOrganizationAsync(Guid organizationId, int page, int pageSize)
        => throw new NotImplementedException();

    public Task<AppointmentResponseDto> CompleteAsync(Guid id, CompleteAppointmentDto dto)
        => throw new NotImplementedException();

    // Private helper — converts the entity to the DTO shape, and is where
    // the enum-to-lowercase-string fix I mentioned earlier actually lives.
    private static AppointmentResponseDto MapToDto(DonationAppointment appointment)
    {
        return new AppointmentResponseDto
        {
            Id = appointment.Id,
            DonorId = appointment.DonorId,
            OrganizationId = appointment.OrganizationId,
            RelatedWorkflowId = appointment.RelatedWorkflowId,
            ScheduledTime = appointment.ScheduledTime,
            Status = appointment.Status.ToString().ToLowerInvariant(), // "Scheduled" -> "scheduled"
            UnitsDonated = appointment.UnitsDonated,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };
    }
}