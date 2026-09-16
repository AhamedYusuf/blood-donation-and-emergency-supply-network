using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Workflows;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/agent/workflows")]
[Authorize]
public class AgentWorkflowsController : ControllerBase
{
    private readonly IAgentWorkflowService _workflowService;

    public AgentWorkflowsController(IAgentWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    // GET /api/agent/workflows/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWorkflow(Guid id)
    {
        var workflow = await _workflowService.GetByIdAsync(id);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        return Ok(new
        {
            workflow.Id,
            workflow.BloodRequestId,
            workflow.Status,
            workflow.RevisionCount,
            workflow.StartedAt,
            workflow.UpdatedAt,
            workflow.CompletedAt,
            workflow.FailureReason
        });
    }

    // GET /api/agent/workflows/{id}/steps
    [HttpGet("{id:guid}/steps")]
    public async Task<IActionResult> GetSteps(Guid id)
    {
        var workflow = await _workflowService.GetByIdAsync(id);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        var steps = await _workflowService.GetStepsAsync(id);

        var response = steps.Select(step => new
        {
            step.Id,
            step.WorkflowId,
            step.AgentName,
            step.StepName,
            step.Status,
            step.InputJson,
            step.OutputJson,
            step.Narrative,
            step.StartedAt,
            step.CompletedAt,
            step.ErrorMessage
        });

        return Ok(response);
    }

    // GET /api/agent/workflows/{id}/summary
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetSummary(Guid id)
    {
        var summary = await _workflowService.GetSummaryAsync(id);

        if (summary == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        return Ok(summary);
    }

    // POST /api/agent/workflows/{id}/approve
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] WorkflowDecisionDto dto)
    {
        var userId = CurrentUserId();

        var workflow = await _workflowService.ApproveAsync(
            id,
            userId,
            dto.Comments);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        return Ok(new
        {
            message = "Workflow approved.",
            workflow.Id,
            workflow.Status,
            workflow.RevisionCount,
            workflow.UpdatedAt
        });
    }

    // POST /api/agent/workflows/{id}/reject
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] WorkflowDecisionDto dto)
    {
        var userId = CurrentUserId();

        var workflow = await _workflowService.RejectAsync(
            id,
            userId,
            dto.Comments);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        return Ok(new
        {
            message = "Workflow rejected.",
            workflow.Id,
            workflow.Status,
            workflow.CompletedAt,
            workflow.UpdatedAt
        });
    }

    // POST /api/agent/workflows/{id}/revise
    [HttpPost("{id:guid}/revise")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> Revise(
        Guid id,
        [FromBody] WorkflowDecisionDto dto)
    {
        var userId = CurrentUserId();

        var workflow = await _workflowService.ReviseAsync(
            id,
            userId,
            dto.Comments);

        if (workflow == null)
        {
            return NotFound(new
            {
                message = "Workflow not found."
            });
        }

        if (workflow.Status == "failed")
        {
            return Ok(new
            {
                message = "Maximum revision limit exceeded. Workflow failed.",
                workflow.Id,
                workflow.Status,
                workflow.RevisionCount,
                workflow.FailureReason,
                workflow.CompletedAt
            });
        }

        return Ok(new
        {
            message = "Workflow revision requested.",
            workflow.Id,
            workflow.Status,
            workflow.RevisionCount,
            workflow.UpdatedAt
        });
    }

    private Guid CurrentUserId()
    {
        return Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}