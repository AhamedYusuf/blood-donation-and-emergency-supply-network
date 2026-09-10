using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext _context;
    private readonly IInventoryService _inventoryService;

    public AppointmentService(AppDbContext context, IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
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

        // Donor's own row is keyed by UserId, NOT by DonorProfile's own Id —
        // appointment.DonorId is a User.Id (set from the JWT sub claim at
        // booking time), so we match on DonorProfiles.UserId here.
        var donorProfile = await _context.DonorProfiles
            .FirstOrDefaultAsync(d => d.UserId == appointment.DonorId);

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // If no donor profile exists we still complete the appointment, but
            // skip the inventory + last-donation-date updates (nothing to key
            // them off). Tighten to a hard failure here if the team decides a
            // profile must always exist by completion time.
            if (donorProfile is not null)
            {
                await _inventoryService.CreateTransactionAsync(
                    new CreateInventoryTransactionRequest(
                        OrganizationId: appointment.OrganizationId,
                        BloodType: ParseBloodType(donorProfile.BloodType),
                        Units: dto.UnitsDonated,
                        TransactionType: InventoryTransactionType.DonationIn,
                        RelatedAppointmentId: appointment.Id),
                    requestingUserId);

                donorProfile.LastDonationDate = DateOnly.FromDateTime(DateTime.UtcNow);
                donorProfile.UpdatedAt = DateTime.UtcNow;
            }

            appointment.Status = AppointmentStatus.Completed;
            appointment.UnitsDonated = dto.UnitsDonated;
            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

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

    // Maps DonorProfile.BloodType (plain string, e.g. "O+") to the
    // Domain.Enums.BloodType enum CreateInventoryTransactionRequest expects.
    private static BloodType ParseBloodType(string bloodType) => bloodType switch
    {
        "A+" => BloodType.APositive,
        "A-" => BloodType.ANegative,
        "B+" => BloodType.BPositive,
        "B-" => BloodType.BNegative,
        "AB+" => BloodType.ABPositive,
        "AB-" => BloodType.ABNegative,
        "O+" => BloodType.OPositive,
        "O-" => BloodType.ONegative,
        _ => throw new ArgumentException($"Unrecognized blood type: '{bloodType}'")
    };
}
