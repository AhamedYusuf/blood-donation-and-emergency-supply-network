using System.Text.Json;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

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
        var step = new AgentStep
        {
            WorkflowId = request.WorkflowId,
            AgentName = "eligibility_validation_agent",
            InputData = JsonSerializer.Serialize(request),
            Status = "running",
            StartedAt = DateTime.UtcNow
        };
        _db.AgentSteps.Add(step);
        await _db.SaveChangesAsync(ct);

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
            step.OutputData = JsonSerializer.Serialize(response);
            step.Status = "completed";
            step.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return Ok(response);
        }
        catch (Exception ex)
        {
            step.Status = "failed";
            step.ErrorMessage = ex.Message;
            step.RetryCount += 1;
            step.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}

public record ValidateEligibilityRequest(Guid WorkflowId, List<Guid> CandidateDonorIds, string RequiredBloodType);
public record EligibleDonorDto(Guid DonorId, decimal ReliabilityScore);
public record ExcludedDonorDto(Guid DonorId, string Reason);
public record ValidateEligibilityResponse(List<EligibleDonorDto> Eligible, List<ExcludedDonorDto> Excluded);