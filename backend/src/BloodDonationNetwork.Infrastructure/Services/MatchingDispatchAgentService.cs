using System.Text.Json;
using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Agents;
using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Services;

public class MatchingDispatchAgentService : IMatchingDispatchAgentService
{
    private readonly AppDbContext _context;
    private readonly DonorRankingCalculator _rankingCalculator;
    private readonly IAppointmentService _appointmentService;
    private readonly INotificationService _notificationService;

    public MatchingDispatchAgentService(
        AppDbContext context,
        DonorRankingCalculator rankingCalculator,
        IAppointmentService appointmentService,
        INotificationService notificationService)
    {
        _context = context;
        _rankingCalculator = rankingCalculator;
        _appointmentService = appointmentService;
        _notificationService = notificationService;
    }

    public async Task<SearchDonorsResponseDto> SearchDonorsAsync(SearchDonorsRequestDto request)
    {
        var startedAt = DateTime.UtcNow;
        var lat = request.Location.Lat;
        var lng = request.Location.Lng;

        // Rough pre-filter by blood type only — real distance filtering
        // happens in-memory below, since Haversine distance can't be
        // translated into SQL by EF Core directly.
        var candidateDonors = await _context.DonorProfiles
            .Where(d => d.BloodType == request.BloodType
                     && d.VerifiedByAdmin
                     && d.Latitude != null
                     && d.Longitude != null)
            .ToListAsync();

        // (distance, score, donor) — score is used only to sort; the
        // public contract (Tech Doc §4.4) exposes the resulting ordinal
        // position as "rank", not the raw score.
        var scored = new List<(double DistanceKm, double Score, Domain.Entities.DonorProfile Donor)>();

        foreach (var donor in candidateDonors)
        {
            var distanceKm = GeoUtils.DistanceKm(
                lat, lng,
                donor.Latitude!.Value, donor.Longitude!.Value);

            if (distanceKm > request.RadiusKm)
            {
                continue;
            }

            var score = _rankingCalculator.CalculateScore(
                distanceKm, request.UrgencyLevel, (double)donor.ReliabilityScore);

            scored.Add((distanceKm, score, donor));
        }

        var ranked = scored
            .OrderByDescending(c => c.Score)
            .Select((c, index) => new DonorCandidateDto
            {
                DonorId = c.Donor.Id,
                DistanceKm = Math.Round(c.DistanceKm, 2),
                ReliabilityScore = c.Donor.ReliabilityScore,
                Rank = index + 1,
            })
            .ToList();

        var response = new SearchDonorsResponseDto { Candidates = ranked };

        await LogStepAsync(
            request.WorkflowId,
            stepName: "search_donors",
            status: "completed",
            input: new { request.BloodType, request.UnitsNeeded, Location = new { lat, lng }, request.RadiusKm, request.UrgencyLevel },
            output: response,
            narrative: $"Found {ranked.Count} candidate donor(s) for {request.BloodType} within {request.RadiusKm}km.",
            startedAt: startedAt);

        return response;
    }

    public async Task<DispatchResponseDto> DispatchAsync(DispatchRequestDto request)
    {
        var startedAt = DateTime.UtcNow;

        var workflow = await _context.AgentWorkflows
            .FirstOrDefaultAsync(w => w.Id == request.WorkflowId)
            ?? throw new KeyNotFoundException($"Workflow {request.WorkflowId} not found.");

        // The required guard (Tech Doc §4.4): dispatch may only run once a
        // staff/admin user has approved the workflow via
        // POST /api/agent/workflows/{id}/approve. Every other status —
        // still planning, awaiting approval, rejected, already
        // dispatched — rejects here rather than silently notifying donors
        // for a request nobody signed off on.
        if (workflow.Status != WorkflowStatuses.Approved)
        {
            var rejectionMessage =
                $"Workflow {workflow.Id} is not approved (status: '{workflow.Status}'). Dispatch is only allowed for approved workflows.";

            await LogStepAsync(
                workflow.Id,
                stepName: "dispatch",
                status: "failed",
                input: new { request.WorkflowId, EligibleCount = request.Eligible.Count },
                output: null,
                narrative: "Dispatch rejected — workflow is not approved.",
                startedAt: startedAt,
                errorMessage: rejectionMessage);

            throw new InvalidOperationException(rejectionMessage);
        }

        var bloodRequest = await _context.BloodRequests
            .FirstOrDefaultAsync(r => r.Id == workflow.BloodRequestId)
            ?? throw new KeyNotFoundException($"Blood request {workflow.BloodRequestId} not found.");

        // How soon the reserved slot is — tighter for a critical request.
        // There's no "donor negotiates a time" concept in the schema yet,
        // so this reserves a real appointment the donor can see and
        // reschedule/cancel from the app, rather than leaving it as an
        // unscheduled "invite" nothing in this codebase represents.
        var window = bloodRequest.Urgency switch
        {
            RequestUrgency.Critical => TimeSpan.FromHours(6),
            RequestUrgency.Urgent => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(72),
        };
        var scheduledTime = DateTime.UtcNow.Add(window);

        // Tech Doc §4.4 Mode 2 output: notifiedDonorIds and
        // appointmentsCreated as ID arrays, failedNotifications as
        // {donorId, reason}. A donor whose appointment can't even be
        // created (bad profile, DB error) is reported the same way as a
        // failed notification — the spec has no separate slot for that,
        // and the caller still needs to know why this candidate produced
        // nothing.
        var notifiedDonorIds = new List<Guid>();
        var appointmentsCreated = new List<Guid>();
        var failedNotifications = new List<FailedNotificationDto>();

        foreach (var candidate in request.Eligible)
        {
            // DispatchCandidateDto.DonorId is a DonorProfile.Id (it's
            // Mode 1's own SearchDonorsResponseDto shape, filtered by the
            // Eligibility Validation Agent) — but appointments and FCM
            // both key off User.Id. Resolve that here rather than
            // pushing the distinction onto every caller.
            var donorProfile = await _context.DonorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == candidate.DonorId);

            if (donorProfile is null)
            {
                failedNotifications.Add(new FailedNotificationDto
                {
                    DonorId = candidate.DonorId,
                    Reason = "Donor profile not found.",
                });
                continue;
            }

            Guid appointmentId;
            try
            {
                var appointment = await _appointmentService.CreateAsync(donorProfile.UserId, new CreateAppointmentDto
                {
                    OrganizationId = bloodRequest.OrganizationId,
                    ScheduledTime = scheduledTime,
                    RelatedWorkflowId = workflow.Id,
                });
                appointmentId = appointment.Id;
            }
            catch (Exception ex)
            {
                failedNotifications.Add(new FailedNotificationDto
                {
                    DonorId = candidate.DonorId,
                    Reason = $"Could not create appointment: {ex.Message}",
                });
                continue;
            }

            appointmentsCreated.Add(appointmentId);

            var notification = await _notificationService.SendToDonorAsync(
                donorProfile.UserId,
                "Urgent blood request matches your type",
                $"{bloodRequest.HospitalName} needs {bloodRequest.BloodType} — you're reserved for {scheduledTime:MMM d, h:mm tt} UTC. Open the app to confirm or reschedule.",
                new Dictionary<string, string>
                {
                    ["workflowId"] = workflow.Id.ToString(),
                    ["appointmentId"] = appointmentId.ToString(),
                });

            if (notification.AnyDelivered)
            {
                notifiedDonorIds.Add(candidate.DonorId);
            }
            else
            {
                failedNotifications.Add(new FailedNotificationDto
                {
                    DonorId = candidate.DonorId,
                    Reason = notification.FailureReason ?? "Notification not delivered.",
                });
            }
        }

        var response = new DispatchResponseDto
        {
            WorkflowId = workflow.Id,
            NotifiedDonorIds = notifiedDonorIds,
            AppointmentsCreated = appointmentsCreated,
            FailedNotifications = failedNotifications,
        };

        await LogStepAsync(
            workflow.Id,
            stepName: "dispatch",
            status: "completed",
            input: new { request.WorkflowId, EligibleCount = request.Eligible.Count },
            output: response,
            narrative: $"Dispatched to {request.Eligible.Count} donor(s): {response.AppointmentsCreated.Count} appointment(s) created, {response.NotifiedDonorIds.Count} notification(s) delivered.",
            startedAt: startedAt);

        return response;
    }

    // Tech Doc §0.7 — every agent action writes a row to the shared
    // agent_steps table. Best-effort: if request.WorkflowId doesn't match
    // a real AgentWorkflow (e.g. an ad-hoc/manual call not part of a
    // tracked workflow run), this silently skips rather than failing the
    // caller's actual request — logging is observability, not a
    // correctness dependency for search/dispatch themselves.
    private async Task LogStepAsync(
        Guid workflowId,
        string stepName,
        string status,
        object? input,
        object? output,
        string narrative,
        DateTime startedAt,
        string? errorMessage = null)
    {
        var workflowExists = await _context.AgentWorkflows
            .AnyAsync(w => w.Id == workflowId);

        if (!workflowExists)
        {
            return;
        }

        _context.AgentSteps.Add(new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            AgentName = AgentNames.MatchingDispatch,
            StepName = stepName,
            Status = status,
            InputJson = input is null ? null : JsonSerializer.Serialize(input),
            OutputJson = output is null ? null : JsonSerializer.Serialize(output),
            Narrative = narrative,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage,
        });

        await _context.SaveChangesAsync();
    }
}