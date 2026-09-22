using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers.Internal;

[ApiController]
[Route("api/internal/agent/workflows")]
public class WorkflowInternalController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public WorkflowInternalController(
        IApplicationDbContext context)
    {
        _context = context;
    }

    // POST /api/internal/agent/workflows
    [HttpPost]
    public async Task<IActionResult> CreateWorkflow(
        [FromBody] CreateWorkflowInternalRequest request)
    {
        var bloodRequest =
            await _context.BloodRequests.FindAsync(
                request.BloodRequestId);

        if (bloodRequest == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        var now = DateTime.UtcNow;

        var bloodType =
            MapBloodType(bloodRequest.BloodType);

        var objective =
            $"Fulfill {bloodRequest.UnitsRequested} unit(s) of " +
            $"{bloodType} blood for {bloodRequest.HospitalName}.";

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            BloodRequestId = request.BloodRequestId,
            Status = WorkflowStatuses.Planning,
            Objective = objective,
            CurrentAgent = AgentNames.Coordinator,
            RevisionCount = 0,
            StartedAt = now,
            UpdatedAt = now
        };

        _context.AgentWorkflows.Add(workflow);

        await _context.SaveChangesAsync();

        return Created(
            $"/api/agent/workflows/{workflow.Id}",
            new
            {
                workflow.Id,
                workflow.BloodRequestId,
                workflow.Status,
                workflow.Objective,
                workflow.CurrentAgent,
                workflow.RevisionCount,
                workflow.StartedAt
            });
    }

    // GET /api/internal/agent/workflows/request/{bloodRequestId}
    [HttpGet("request/{bloodRequestId:guid}")]
    public async Task<IActionResult> GetBloodRequestForAgent(
        Guid bloodRequestId)
    {
        var bloodRequest =
            await _context.BloodRequests.FindAsync(
                bloodRequestId);

        if (bloodRequest == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        var bloodType =
            MapBloodType(bloodRequest.BloodType);

        var urgency =
            MapUrgency(bloodRequest.Urgency);

        return Ok(new
        {
            bloodRequestId = bloodRequest.Id,
            organizationId = bloodRequest.OrganizationId,
            bloodType,
            unitsRequested =
                bloodRequest.UnitsRequested,
            urgency,
            latitude = bloodRequest.Latitude,
            longitude = bloodRequest.Longitude
        });
    }

    // POST /api/internal/agent/workflows/{id}/status
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody]
        UpdateWorkflowStatusInternalRequest request)
    {
        var workflow =
            await _context.AgentWorkflows.FindAsync(id);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        workflow.Status = request.Status;
        workflow.UpdatedAt = DateTime.UtcNow;

        if (request.Status == WorkflowStatuses.Completed ||
            request.Status == WorkflowStatuses.Failed ||
            request.Status == WorkflowStatuses.Rejected)
        {
            workflow.CompletedAt = DateTime.UtcNow;

            // Terminal state: no active agent.
            workflow.CurrentAgent = null;
        }

        if (!string.IsNullOrWhiteSpace(
                request.FailureReason))
        {
            workflow.FailureReason =
                request.FailureReason;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            workflow.Id,
            workflow.Status,
            workflow.Objective,
            workflow.CurrentAgent,
            workflow.UpdatedAt,
            workflow.CompletedAt,
            workflow.FailureReason
        });
    }

    // POST /api/internal/agent/workflows/{id}/steps
    [HttpPost("{id:guid}/steps")]
    public async Task<IActionResult> AddStep(
        Guid id,
        [FromBody]
        CreateAgentStepInternalRequest request)
    {
        var workflow =
            await _context.AgentWorkflows.FindAsync(id);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        var step = new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = id,
            AgentName = request.AgentName,
            StepName = request.StepName,
            Status = request.Status,
            InputJson = request.InputJson,
            OutputJson = request.OutputJson,
            Narrative = request.Narrative,
            RetryCount = request.RetryCount,
            StartedAt =
                request.StartedAt ?? DateTime.UtcNow,
            CompletedAt = request.CompletedAt,
            ErrorMessage = request.ErrorMessage
        };

        _context.AgentSteps.Add(step);

        workflow.CurrentAgent =
            request.AgentName;

        workflow.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            step.Id,
            step.WorkflowId,
            step.AgentName,
            step.StepName,
            step.Status,
            step.RetryCount,
            step.StartedAt,
            step.CompletedAt,
            workflow.CurrentAgent
        });
    }

    private static string MapBloodType(
        BloodType bloodType)
    {
        return bloodType switch
        {
            BloodType.APositive =>
                BloodTypes.APositive,

            BloodType.ANegative =>
                BloodTypes.ANegative,

            BloodType.BPositive =>
                BloodTypes.BPositive,

            BloodType.BNegative =>
                BloodTypes.BNegative,

            BloodType.ABPositive =>
                BloodTypes.ABPositive,

            BloodType.ABNegative =>
                BloodTypes.ABNegative,

            BloodType.OPositive =>
                BloodTypes.OPositive,

            BloodType.ONegative =>
                BloodTypes.ONegative,

            _ => throw new ArgumentOutOfRangeException(
                nameof(bloodType),
                bloodType,
                "Unsupported blood type.")
        };
    }

    private static string MapUrgency(
        RequestUrgency urgency)
    {
        return urgency switch
        {
            RequestUrgency.Normal =>
                "routine",

            RequestUrgency.Urgent =>
                "urgent",

            RequestUrgency.Critical =>
                "critical",

            _ => throw new ArgumentOutOfRangeException(
                nameof(urgency),
                urgency,
                "Unsupported request urgency.")
        };
    }
}

public class CreateWorkflowInternalRequest
{
    public Guid BloodRequestId { get; set; }
}

public class UpdateWorkflowStatusInternalRequest
{
    public string Status { get; set; } =
        string.Empty;

    public string? FailureReason { get; set; }
}

public class CreateAgentStepInternalRequest
{
    public string AgentName { get; set; } =
        string.Empty;

    public string StepName { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        string.Empty;

    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public string? Narrative { get; set; }

    public int RetryCount { get; set; } = 0;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }
}