using System.Text.Json;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Api.Controllers.Internal;

[ApiController]
[Route("api/internal/agent")]
public class EligibilityAgentController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly IEligibilityRuleEngine _eligibilityEngine;

    public EligibilityAgentController(IApplicationDbContext db, IEligibilityRuleEngine eligibilityEngine)
    {
        _db = db;
        _eligibilityEngine = eligibilityEngine;
    }

    [HttpPost("validate-eligibility")]
    public async Task<ActionResult<ValidateEligibilityResponse>> ValidateEligibility(
        ValidateEligibilityRequest request, CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            var eligible = new List<EligibleDonorDto>();
            var excluded = new List<ExcludedDonorDto>();

            foreach (var donorId in request.CandidateDonorIds)
            {
                var donor = await _db.DonorProfiles.FindAsync([donorId], ct);
                if (donor is null)
                {
                    excluded.Add(new(donorId, "donor not found"));
                    continue;
                }

                var (isEligible, reason) = _eligibilityEngine.Evaluate(donor, request.RequiredBloodType);
                if (isEligible) eligible.Add(new(donorId, donor.ReliabilityScore));
                else excluded.Add(new(donorId, reason!));
            }

            var response = new ValidateEligibilityResponse(eligible, excluded);

            await LogStepAsync(
                request.WorkflowId,
                status: "completed",
                inputJson: JsonSerializer.Serialize(request),
                outputJson: JsonSerializer.Serialize(response),
                startedAt: startedAt,
                errorMessage: null,
                ct: ct);

            return Ok(response);
        }
        catch (Exception ex)
        {
            await LogStepAsync(
                request.WorkflowId,
                status: "failed",
                inputJson: JsonSerializer.Serialize(request),
                outputJson: null,
                startedAt: startedAt,
                errorMessage: ex.Message,
                ct: CancellationToken.None);
            throw;
        }
    }

    // Tech Doc §0.7 — every agent action writes a row to the shared
    // agent_steps table. Best-effort: if WorkflowId doesn't match a real
    // AgentWorkflow (an ad-hoc/manual call not part of a tracked
    // workflow run), this silently skips rather than failing the
    // caller's actual eligibility check — logging is observability, not
    // a correctness dependency for validation itself.
    private async Task LogStepAsync(
        Guid workflowId,
        string status,
        string? inputJson,
        string? outputJson,
        DateTime startedAt,
        string? errorMessage,
        CancellationToken ct)
    {
        var workflowExists = await _db.AgentWorkflows
            .AnyAsync(w => w.Id == workflowId, ct);

        if (!workflowExists)
        {
            return;
        }

        _db.AgentSteps.Add(new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            AgentName = AgentNames.EligibilityValidation,
            StepName = "validate_eligibility",
            Status = status,
            InputJson = inputJson,
            OutputJson = outputJson,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage,
        });

        await _db.SaveChangesAsync(ct);
    }
}

public record ValidateEligibilityRequest(Guid WorkflowId, List<Guid> CandidateDonorIds, string RequiredBloodType);
public record EligibleDonorDto(Guid DonorId, decimal ReliabilityScore);
public record ExcludedDonorDto(Guid DonorId, string Reason);
public record ValidateEligibilityResponse(List<EligibleDonorDto> Eligible, List<ExcludedDonorDto> Excluded);