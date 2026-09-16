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

    public WorkflowInternalController(IApplicationDbContext context)
    {
        _context = context;
    }

    // POST /api/internal/agent/workflows
    [HttpPost]
    public async Task<IActionResult> CreateWorkflow(
        [FromBody] CreateWorkflowInternalRequest request)
    {
        var bloodRequest = await _context.BloodRequests.FindAsync(
            request.BloodRequestId);

        if (bloodRequest == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        var now = DateTime.UtcNow;

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            BloodRequestId = request.BloodRequestId,
            Status = WorkflowStatuses.Planning,
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
                workflow.RevisionCount,
                workflow.StartedAt
            });
    }

    // GET /api/internal/agent/workflows/request/{bloodRequestId}
    [HttpGet("request/{bloodRequestId:guid}")]
    public async Task<IActionResult> GetBloodRequestForAgent(
        Guid bloodRequestId)
    {
        var bloodRequest = await _context.BloodRequests.FindAsync(
            bloodRequestId);

        if (bloodRequest == null)
        {
            return NotFound(new
            {
                message = "Blood request not found."
            });
        }

        return Ok(new
        {
            bloodRequestId = bloodRequest.Id,
            bloodType = bloodRequest.BloodType,
            unitsRequested = bloodRequest.UnitsRequested,
            urgency = bloodRequest.Urgency,
            latitude = bloodRequest.Latitude,
            longitude = bloodRequest.Longitude
        });
    }

    // POST /api/internal/agent/workflows/{id}/status
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateWorkflowStatusInternalRequest request)
    {
        var workflow = await _context.AgentWorkflows.FindAsync(id);

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
        }

        if (!string.IsNullOrWhiteSpace(request.FailureReason))
        {
            workflow.FailureReason = request.FailureReason;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            workflow.Id,
            workflow.Status,
            workflow.UpdatedAt,
            workflow.CompletedAt,
            workflow.FailureReason
        });
    }

    // POST /api/internal/agent/workflows/{id}/steps
    [HttpPost("{id:guid}/steps")]
    public async Task<IActionResult> AddStep(
        Guid id,
        [FromBody] CreateAgentStepInternalRequest request)
    {
        var workflow = await _context.AgentWorkflows.FindAsync(id);

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
            StartedAt = request.StartedAt ?? DateTime.UtcNow,
            CompletedAt = request.CompletedAt,
            ErrorMessage = request.ErrorMessage
        };

        _context.AgentSteps.Add(step);

        workflow.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            step.Id,
            step.WorkflowId,
            step.AgentName,
            step.StepName,
            step.Status,
            step.StartedAt,
            step.CompletedAt
        });
    }
}

public class CreateWorkflowInternalRequest
{
    public Guid BloodRequestId { get; set; }
}

public class UpdateWorkflowStatusInternalRequest
{
    public string Status { get; set; } = string.Empty;

    public string? FailureReason { get; set; }
}

public class CreateAgentStepInternalRequest
{
    public string AgentName { get; set; } = string.Empty;

    public string StepName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public string? Narrative { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }
}