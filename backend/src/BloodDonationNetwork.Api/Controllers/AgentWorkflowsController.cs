using System.Net.Http.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentWorkflowsController(
        IAgentWorkflowService workflowService,
        IHttpClientFactory httpClientFactory)
    {
        _workflowService = workflowService;
        _httpClientFactory = httpClientFactory;
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

        // First save the approval in PostgreSQL.
        // Dispatch checks the DB workflow status, so the workflow must
        // already be approved before LangGraph continues to dispatch.
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

        var resumeResult = await ResumePythonWorkflowAsync(
            id,
            "approve",
            dto.Comments);

        if (!resumeResult.Success)
        {
            return StatusCode(502, new
            {
                message =
                    "Workflow was approved in the database, but the agent workflow could not be resumed.",
                workflow.Id,
                workflow.Status,
                agentError = resumeResult.Error
            });
        }

        return Ok(new
        {
            message = "Workflow approved and agent resumed.",
            workflow.Id,
            workflow.Status,
            workflow.RevisionCount,
            workflow.UpdatedAt,
            agentResumed = true
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

        var resumeResult = await ResumePythonWorkflowAsync(
            id,
            "reject",
            dto.Comments);

        if (!resumeResult.Success)
        {
            return StatusCode(502, new
            {
                message =
                    "Workflow was rejected in the database, but the agent workflow could not be resumed.",
                workflow.Id,
                workflow.Status,
                agentError = resumeResult.Error
            });
        }

        return Ok(new
        {
            message = "Workflow rejected and agent resumed.",
            workflow.Id,
            workflow.Status,
            workflow.CompletedAt,
            workflow.UpdatedAt,
            agentResumed = true
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

        // Revision limit reached.
        // There is nothing to resume because the workflow is now failed.
        if (workflow.Status == "failed")
        {
            return Ok(new
            {
                message =
                    "Maximum revision limit exceeded. Workflow failed.",
                workflow.Id,
                workflow.Status,
                workflow.RevisionCount,
                workflow.FailureReason,
                workflow.CompletedAt
            });
        }

        var resumeResult = await ResumePythonWorkflowAsync(
            id,
            "revise",
            dto.Comments);

        if (!resumeResult.Success)
        {
            return StatusCode(502, new
            {
                message =
                    "Revision was recorded in the database, but the agent workflow could not be resumed.",
                workflow.Id,
                workflow.Status,
                workflow.RevisionCount,
                agentError = resumeResult.Error
            });
        }

        return Ok(new
        {
            message = "Workflow revision requested and agent resumed.",
            workflow.Id,
            workflow.Status,
            workflow.RevisionCount,
            workflow.UpdatedAt,
            agentResumed = true
        });
    }

    private async Task<AgentResumeResult> ResumePythonWorkflowAsync(
        Guid workflowId,
        string decision,
        string? comments)
    {
        try
        {
            var client =
                _httpClientFactory.CreateClient("AgentService");

            var response = await client.PostAsJsonAsync(
                $"/resume-workflow/{workflowId}",
                new
                {
                    decision,
                    comments
                });

            if (response.IsSuccessStatusCode)
            {
                return new AgentResumeResult(
                    true,
                    null);
            }

            var errorBody =
                await response.Content.ReadAsStringAsync();

            return new AgentResumeResult(
                false,
                errorBody);
        }
        catch (HttpRequestException ex)
        {
            return new AgentResumeResult(
                false,
                ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            return new AgentResumeResult(
                false,
                ex.Message);
        }
    }

    private Guid CurrentUserId()
    {
        return Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private record AgentResumeResult(
        bool Success,
        string? Error);
}