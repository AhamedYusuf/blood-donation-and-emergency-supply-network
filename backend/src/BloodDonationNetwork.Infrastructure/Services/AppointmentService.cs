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

    public async Task<AppointmentResponseDto> UpdateStatusAsync(Guid id, UpdateAppointmentStatusDto dto)
    {
        var appointment = await _context.DonationAppointments
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
        {
            throw new KeyNotFoundException($"Appointment {id} not found.");
        }

        appointment.Status = ParseStatus(dto.NewStatus);
        appointment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(appointment);
    }

    public async Task<List<AppointmentResponseDto>> GetByDonorAsync(Guid donorId)
{
    var appointments = await _context.DonationAppointments
        .Where(a => a.DonorId == donorId)
        .OrderByDescending(a => a.ScheduledTime)
        .ToListAsync();

    return appointments.Select(MapToDto).ToList();
}

    public async Task<PagedResultDto<AppointmentResponseDto>> GetUpcomingByOrganizationAsync(
    Guid organizationId, int page, int pageSize, Guid requestingUserId, bool isAdmin)
{
    if (!isAdmin)
    {
        var requestingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == requestingUserId);

        if (requestingUser?.OrganizationId != organizationId)
        {
            throw new UnauthorizedAccessException(
                "Staff may only view upcoming appointments for their own organization.");
        }
    }

    var query = _context.DonationAppointments
        .Where(a => a.OrganizationId == organizationId
                 && a.ScheduledTime > DateTime.UtcNow
                 && a.Status != AppointmentStatus.Cancelled)
        .OrderBy(a => a.ScheduledTime);

    var totalCount = await query.CountAsync();

    var appointments = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResultDto<AppointmentResponseDto>
    {
        Items = appointments.Select(MapToDto).ToList(),
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount
    };
}
    public async Task<AppointmentResponseDto> CompleteAsync(
    Guid id, CompleteAppointmentDto dto, Guid requestingUserId, bool isAdmin)
{
    var appointment = await _context.DonationAppointments
        .FirstOrDefaultAsync(a => a.Id == id);

    if (appointment is null)
    {
        throw new KeyNotFoundException($"Appointment {id} not found.");
    }

    if (!isAdmin)
    {
        var requestingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == requestingUserId);

        if (requestingUser?.OrganizationId != appointment.OrganizationId)
        {
            throw new UnauthorizedAccessException(
                "Staff may only complete appointments at their own organization.");
        }
    }

    // TODO (blocked — waiting on Student 3's InventoryService):
    // call inventory transaction logic here (transaction_type = "donation_in",
    // units = dto.UnitsDonated, related_appointment_id = appointment.Id),
    // in the SAME database transaction as the appointment update below,
    // per Tech Doc §4.3's cross-table atomicity requirement.

    // TODO (blocked — waiting on Student 1's DonorProfile entity):
    // update donor_profiles.last_donation_date = DateTime.UtcNow
    // for appointment.DonorId, in the same transaction.

    appointment.Status = AppointmentStatus.Completed;
    appointment.UnitsDonated = dto.UnitsDonated;
    appointment.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return MapToDto(appointment);
}

    // Private helper — converts the entity to the DTO shape.
    private static AppointmentResponseDto MapToDto(DonationAppointment appointment)
    {
        return new AppointmentResponseDto
        {
            Id = appointment.Id,
            DonorId = appointment.DonorId,
            OrganizationId = appointment.OrganizationId,
            RelatedWorkflowId = appointment.RelatedWorkflowId,
            ScheduledTime = appointment.ScheduledTime,
            Status = MapStatusToString(appointment.Status),
            UnitsDonated = appointment.UnitsDonated,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };
    }

    // Enum -> exact lowercase/snake_case string per Tech Doc §0.5
    private static string MapStatusToString(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => "scheduled",
        AppointmentStatus.Completed => "completed",
        AppointmentStatus.NoShow => "no_show",
        AppointmentStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown appointment status")
    };

    // Reverse direction — client-sent string -> enum
    private static AppointmentStatus ParseStatus(string status) => status switch
    {
        "scheduled" => AppointmentStatus.Scheduled,
        "completed" => AppointmentStatus.Completed,
        "no_show" => AppointmentStatus.NoShow,
        "cancelled" => AppointmentStatus.Cancelled,
        _ => throw new ArgumentException($"Invalid appointment status: '{status}'")
    };
}