using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Agents;
using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
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
        // Rough pre-filter by blood type only — real distance filtering
        // happens in-memory below, since Haversine distance can't be
        // translated into SQL by EF Core directly.
        var candidateDonors = await _context.DonorProfiles
            .Where(d => d.BloodType == request.BloodType
                     && d.VerifiedByAdmin
                     && d.Latitude != null
                     && d.Longitude != null)
            .ToListAsync();

        var candidates = new List<DonorCandidateDto>();

        foreach (var donor in candidateDonors)
        {
            var distanceKm = GeoUtils.DistanceKm(
                request.Latitude, request.Longitude,
                donor.Latitude!.Value, donor.Longitude!.Value);

            if (distanceKm > request.RadiusKm)
            {
                continue;
            }

            var score = _rankingCalculator.CalculateScore(
                distanceKm, request.UrgencyLevel, (double)donor.ReliabilityScore);

            candidates.Add(new DonorCandidateDto
            {
                DonorId = donor.Id,
                DistanceKm = Math.Round(distanceKm, 2),
                Score = Math.Round(score, 4),
                ReliabilityScore = donor.ReliabilityScore
            });
        }

        var ranked = candidates.OrderByDescending(c => c.Score).ToList();

        return new SearchDonorsResponseDto { Candidates = ranked };
    }

    public async Task<DispatchResponseDto> DispatchAsync(DispatchRequestDto request)
    {
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
            throw new InvalidOperationException(
                $"Workflow {workflow.Id} is not approved (status: '{workflow.Status}'). Dispatch is only allowed for approved workflows.");
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

        var results = new List<DispatchDonorResultDto>();

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
                results.Add(new DispatchDonorResultDto
                {
                    DonorId = candidate.DonorId,
                    FailureReason = "Donor profile not found.",
                });
                continue;
            }

            Guid? appointmentId = null;
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
                results.Add(new DispatchDonorResultDto
                {
                    DonorId = candidate.DonorId,
                    FailureReason = $"Could not create appointment: {ex.Message}",
                });
                continue;
            }

            var notification = await _notificationService.SendToDonorAsync(
                donorProfile.UserId,
                "Urgent blood request matches your type",
                $"{bloodRequest.HospitalName} needs {bloodRequest.BloodType} — you're reserved for {scheduledTime:MMM d, h:mm tt} UTC. Open the app to confirm or reschedule.",
                new Dictionary<string, string>
                {
                    ["workflowId"] = workflow.Id.ToString(),
                    ["appointmentId"] = appointmentId?.ToString() ?? string.Empty,
                });

            results.Add(new DispatchDonorResultDto
            {
                DonorId = candidate.DonorId,
                AppointmentId = appointmentId,
                NotificationDelivered = notification.AnyDelivered,
                FailureReason = notification.AnyDelivered ? null : notification.FailureReason,
            });
        }

        return new DispatchResponseDto
        {
            WorkflowId = workflow.Id,
            DonorsContacted = request.Eligible.Count,
            AppointmentsCreated = results.Count(r => r.AppointmentId.HasValue),
            NotificationsDelivered = results.Count(r => r.NotificationDelivered),
            Results = results,
        };
    }
}