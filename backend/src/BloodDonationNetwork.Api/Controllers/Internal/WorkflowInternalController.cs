using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Api.Controllers.Internal;

[ApiController]
[Route("api/internal/agent/workflows")]
public class WorkflowInternalController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<WorkflowInternalController> _logger;

    public WorkflowInternalController(
        IApplicationDbContext context,
        ILogger<WorkflowInternalController> logger)
    {
        _context = context;
        _logger = logger;
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

        var existingWorkflow =
            await _context.AgentWorkflows
                .Where(workflow =>
                    workflow.BloodRequestId ==
                    request.BloodRequestId)
                .OrderByDescending(workflow =>
                    workflow.StartedAt)
                .FirstOrDefaultAsync();

        if (existingWorkflow is not null)
        {
            return Ok(new
            {
                existingWorkflow.Id,
                existingWorkflow.BloodRequestId,
                existingWorkflow.Status,
                existingWorkflow.Objective,
                existingWorkflow.CurrentAgent,
                existingWorkflow.RevisionCount,
                existingWorkflow.StartedAt,
                alreadyExists = true
            });
        }

        var now = DateTime.UtcNow;

        var bloodType =
            MapBloodType(bloodRequest.BloodType);

        var hospitalName = bloodRequest.HospitalName;
        if (string.IsNullOrWhiteSpace(hospitalName))
        {
            hospitalName = await _context.Organizations
                .Where(organization => organization.Id == bloodRequest.OrganizationId)
                .Select(organization => organization.Name)
                .FirstOrDefaultAsync() ?? "the requesting hospital";
        }

        var objective =
            $"Fulfill {bloodRequest.UnitsRequested} unit(s) of " +
            $"{bloodType} blood for {hospitalName}.";

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
                workflow.StartedAt,
                alreadyExists = false
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

        var bloodRequest =
            await _context.BloodRequests.FindAsync(
                workflow.BloodRequestId);
        var oldRequestStatus =
            bloodRequest?.Status;

        workflow.Status = request.Status;
        workflow.UpdatedAt = DateTime.UtcNow;

        // When the workflow pauses for human approval, the Coordinator
        // owns the active workflow state. Do not leave CurrentAgent
        // pointing at the last worker agent that logged a step.
        if (request.Status == WorkflowStatuses.AwaitingApproval)
        {
            workflow.CurrentAgent = AgentNames.Coordinator;

            if (bloodRequest?.Status == RequestStatuses.Open ||
                bloodRequest?.Status == RequestStatuses.Matching)
            {
                bloodRequest.Status =
                    RequestStatuses.AwaitingApproval;
            }
        }

        if (request.Status == WorkflowStatuses.Completed)
        {
            if (bloodRequest is not null &&
                (bloodRequest.Status == RequestStatuses.Open ||
                 bloodRequest.Status == RequestStatuses.Matching ||
                 bloodRequest.Status == RequestStatuses.AwaitingApproval))
            {
                bloodRequest.Status =
                    RequestStatuses.DonorsNotified;
            }
        }

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

        var savedChanges =
            await _context.SaveChangesAsync();

        var persistedRequestStatus =
            await _context.BloodRequests
                .AsNoTracking()
                .Where(item =>
                    item.Id == workflow.BloodRequestId)
                .Select(item => item.Status)
                .FirstOrDefaultAsync();

        _logger.LogInformation(
            "Workflow status callback persisted. WorkflowId={WorkflowId}, " +
            "BloodRequestId={BloodRequestId}, WorkflowStatus={WorkflowStatus}, " +
            "OldBloodRequestStatus={OldBloodRequestStatus}, " +
            "NewBloodRequestStatus={NewBloodRequestStatus}, " +
            "PersistedBloodRequestStatus={PersistedBloodRequestStatus}, " +
            "SaveChangesAffected={SaveChangesAffected}",
            workflow.Id,
            workflow.BloodRequestId,
            workflow.Status,
            oldRequestStatus,
            bloodRequest?.Status,
            persistedRequestStatus,
            savedChanges);

        if (bloodRequest is not null &&
            persistedRequestStatus != bloodRequest.Status)
        {
            _logger.LogError(
                "Blood request status did not persist with workflow callback. " +
                "WorkflowId={WorkflowId}, BloodRequestId={BloodRequestId}, " +
                "ExpectedStatus={ExpectedStatus}, PersistedStatus={PersistedStatus}",
                workflow.Id,
                workflow.BloodRequestId,
                bloodRequest.Status,
                persistedRequestStatus);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message =
                        "Workflow status was saved, but the linked blood request " +
                        "status was not persisted."
                });
        }

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