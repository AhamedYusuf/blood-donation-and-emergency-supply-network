using System.Text.Json;
using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Agents;
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

        return MapToDto(appointment, await GetDonorBloodTypeAsync(donorId));
    }

    public async Task<AppointmentResponseDto?> GetByIdAsync(Guid id)
    {
        var appointment = await _context.DonationAppointments
            .FirstOrDefaultAsync(a => a.Id == id);

        return appointment is null
            ? null
            : MapToDto(appointment, await GetDonorBloodTypeAsync(appointment.DonorId));
    }

    public async Task<AppointmentResponseDto> UpdateStatusAsync(Guid id, UpdateAppointmentStatusDto dto)
    {
        var appointment = await _context.DonationAppointments
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment is null)
        {
            throw new KeyNotFoundException($"Appointment {id} not found.");
        }

        appointment.Status = AppointmentStatusMap.Parse(dto.NewStatus);
        appointment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(appointment, await GetDonorBloodTypeAsync(appointment.DonorId));
    }

    public async Task<List<AppointmentResponseDto>> GetByDonorAsync(Guid donorId)
    {
        var appointments = await _context.DonationAppointments
            .Where(a => a.DonorId == donorId)
            .OrderByDescending(a => a.ScheduledTime)
            .ToListAsync();

        // Every row is the same donor, so one lookup covers the whole list.
        var bloodType = await GetDonorBloodTypeAsync(donorId);

        return appointments.Select(a => MapToDto(a, bloodType)).ToList();
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

        // Batch-load donor profiles (blood type + profile id) for the
        // donors on this page to avoid an N+1 lookup per row. Profile id
        // (not the User id DonationAppointment.DonorId actually stores)
        // is what the agent's own candidate list keys donors by.
        var donorIds = appointments.Select(a => a.DonorId).Distinct().ToList();
        var donorProfiles = await _context.DonorProfiles
            .Where(d => donorIds.Contains(d.UserId))
            .ToDictionaryAsync(d => d.UserId, d => new DonorProfileLookup(d.Id, d.BloodType));

        var agentMatches = await GetAgentMatchesAsync(appointments, donorProfiles);

        return new PagedResultDto<AppointmentResponseDto>
        {
            Items = appointments
                .Select(a => MapToDto(
                    a,
                    donorProfiles.TryGetValue(a.DonorId, out var dp) ? dp.BloodType : null,
                    agentMatches.GetValueOrDefault(a.Id)))
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // Reads back the Matching & Dispatch Agent's own "search_donors"
    // agent_steps row for each appointment's workflow, so the console's
    // "Agent reasoning" panel can show the actual rank/distance/
    // reliability the agent used, not a re-derived guess. Best-effort:
    // any appointment without a related workflow, without a matching
    // step, without a DonorProfile, or whose step JSON doesn't parse
    // simply gets no AgentMatchDto — this is a display enrichment, never
    // a reason to fail the appointment query itself.
    private async Task<Dictionary<Guid, AgentMatchDto>> GetAgentMatchesAsync(
        List<DonationAppointment> appointments,
        Dictionary<Guid, DonorProfileLookup> donorProfilesByUserId)
    {
        var result = new Dictionary<Guid, AgentMatchDto>();

        var workflowIds = appointments
            .Where(a => a.RelatedWorkflowId.HasValue)
            .Select(a => a.RelatedWorkflowId!.Value)
            .Distinct()
            .ToList();

        if (workflowIds.Count == 0)
        {
            return result;
        }

        // A workflow could in theory be searched more than once across
        // revisions — take the latest search_donors step per workflow.
        var steps = await _context.AgentSteps
            .Where(s => workflowIds.Contains(s.WorkflowId)
                     && s.StepName == "search_donors"
                     && s.OutputJson != null)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new { s.WorkflowId, s.OutputJson })
            .ToListAsync();

        var latestOutputByWorkflow = new Dictionary<Guid, string>();
        foreach (var step in steps)
        {
            latestOutputByWorkflow.TryAdd(step.WorkflowId, step.OutputJson!);
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var appointment in appointments)
        {
            if (appointment.RelatedWorkflowId is not { } workflowId)
            {
                continue;
            }

            if (!latestOutputByWorkflow.TryGetValue(workflowId, out var outputJson))
            {
                continue;
            }

            if (!donorProfilesByUserId.TryGetValue(appointment.DonorId, out var donorProfile))
            {
                continue;
            }

            SearchDonorsResponseDto? response;
            try
            {
                response = JsonSerializer.Deserialize<SearchDonorsResponseDto>(outputJson, jsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            var candidate = response?.Candidates.FirstOrDefault(c => c.DonorId == donorProfile.Id);

            if (candidate is null)
            {
                continue;
            }

            result[appointment.Id] = new AgentMatchDto
            {
                Rank = candidate.Rank,
                DistanceKm = candidate.DistanceKm,
                ReliabilityScore = candidate.ReliabilityScore,
            };
        }

        return result;
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

        return MapToDto(appointment, donorProfile?.BloodType);
    }

    // Looks up one donor's blood type by their user id (DonationAppointment.DonorId
    // holds a User.Id). Returns null when the donor has no DonorProfile row.
    private Task<string?> GetDonorBloodTypeAsync(Guid donorUserId) =>
        _context.DonorProfiles
            .Where(d => d.UserId == donorUserId)
            .Select(d => (string?)d.BloodType)
            .FirstOrDefaultAsync();

    // Private helper — converts the entity to the DTO shape.
    private static AppointmentResponseDto MapToDto(
        DonationAppointment appointment, string? donorBloodType = null, AgentMatchDto? agentMatch = null)
    {
        return new AppointmentResponseDto
        {
            Id = appointment.Id,
            DonorId = appointment.DonorId,
            OrganizationId = appointment.OrganizationId,
            RelatedWorkflowId = appointment.RelatedWorkflowId,
            ScheduledTime = appointment.ScheduledTime,
            Status = AppointmentStatusMap.ToApiString(appointment.Status),
            DonorBloodType = donorBloodType,
            AgentMatch = agentMatch,
            UnitsDonated = appointment.UnitsDonated,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt
        };
    }

    // Used only by GetAgentMatchesAsync's batch lookup — pairs the two
    // DonorProfile fields that lookup needs (its own Id, for matching
    // against the agent's candidate list, and BloodType, already needed
    // for the DTO's DonorBloodType).
    private sealed record DonorProfileLookup(Guid Id, string BloodType);

    // Status <-> API-string conversion lives in AppointmentStatusMap
    // (Application/Common) so it can be unit-tested without a DbContext.

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
