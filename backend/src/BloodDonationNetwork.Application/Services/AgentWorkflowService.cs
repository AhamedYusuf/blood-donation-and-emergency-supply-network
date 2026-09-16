using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Services;

public class AgentWorkflowService : IAgentWorkflowService
{
    private readonly IApplicationDbContext _context;

    public AgentWorkflowService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AgentWorkflow?> GetByIdAsync(Guid workflowId)
    {
        return await _context.AgentWorkflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workflowId);
    }

    public async Task<List<AgentStep>> GetStepsAsync(Guid workflowId)
    {
        return await _context.AgentSteps
            .AsNoTracking()
            .Where(s => s.WorkflowId == workflowId)
            .OrderBy(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<object?> GetSummaryAsync(Guid workflowId)
    {
        var workflow = await _context.AgentWorkflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workflowId);

        if (workflow == null)
        {
            return null;
        }

        var steps = await _context.AgentSteps
            .AsNoTracking()
            .Where(s => s.WorkflowId == workflowId)
            .ToListAsync();

        var decisions = await _context.ApprovalDecisions
            .AsNoTracking()
            .Where(d => d.WorkflowId == workflowId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var completedSteps = steps.Count(s =>
            s.CompletedAt != null &&
            string.IsNullOrWhiteSpace(s.ErrorMessage));

        var failedSteps = steps.Count(s =>
            !string.IsNullOrWhiteSpace(s.ErrorMessage));

        var latestDecision = decisions.FirstOrDefault();

        return new
        {
            workflow.Id,
            workflow.BloodRequestId,
            workflow.Status,
            workflow.RevisionCount,
            workflow.StartedAt,
            workflow.UpdatedAt,
            workflow.CompletedAt,
            workflow.FailureReason,

            TotalSteps = steps.Count,
            CompletedSteps = completedSteps,
            FailedSteps = failedSteps,

            LatestDecision = latestDecision == null
                ? null
                : new
                {
                    latestDecision.Decision,
                    latestDecision.Comments,
                    latestDecision.DecidedByUserId,
                    latestDecision.CreatedAt
                }
        };
    }

    public async Task<AgentWorkflow?> ApproveAsync(
        Guid workflowId,
        Guid decidedByUserId,
        string? comments)
    {
        var workflow = await _context.AgentWorkflows
            .FirstOrDefaultAsync(w => w.Id == workflowId);

        if (workflow == null)
        {
            return null;
        }

        EnsureDecisionAllowed(workflow);

        var now = DateTime.UtcNow;

        workflow.Status = WorkflowStatuses.Approved;
        workflow.UpdatedAt = now;

        var decision = new ApprovalDecision
        {
            WorkflowId = workflow.Id,
            DecidedByUserId = decidedByUserId,
            Decision = ApprovalDecisions.Approved,
            Comments = comments,
            CreatedAt = now
        };

        _context.ApprovalDecisions.Add(decision);

        await _context.SaveChangesAsync();

        return workflow;
    }

    public async Task<AgentWorkflow?> RejectAsync(
        Guid workflowId,
        Guid decidedByUserId,
        string? comments)
    {
        var workflow = await _context.AgentWorkflows
            .FirstOrDefaultAsync(w => w.Id == workflowId);

        if (workflow == null)
        {
            return null;
        }

        EnsureDecisionAllowed(workflow);

        var now = DateTime.UtcNow;

        workflow.Status = WorkflowStatuses.Rejected;
        workflow.UpdatedAt = now;
        workflow.CompletedAt = now;

        var decision = new ApprovalDecision
        {
            WorkflowId = workflow.Id,
            DecidedByUserId = decidedByUserId,
            Decision = ApprovalDecisions.Rejected,
            Comments = comments,
            CreatedAt = now
        };

        _context.ApprovalDecisions.Add(decision);

        await _context.SaveChangesAsync();

        return workflow;
    }

    public async Task<AgentWorkflow?> ReviseAsync(
        Guid workflowId,
        Guid decidedByUserId,
        string? comments)
    {
        var workflow = await _context.AgentWorkflows
            .FirstOrDefaultAsync(w => w.Id == workflowId);

        if (workflow == null)
        {
            return null;
        }

        EnsureDecisionAllowed(workflow);

        var now = DateTime.UtcNow;

        if (workflow.RevisionCount >= 3)
        {
            workflow.Status = WorkflowStatuses.Failed;
            workflow.FailureReason =
                "Maximum workflow revision limit exceeded.";
            workflow.CompletedAt = now;
            workflow.UpdatedAt = now;

            var failedDecision = new ApprovalDecision
            {
                WorkflowId = workflow.Id,
                DecidedByUserId = decidedByUserId,
                Decision = ApprovalDecisions.RevisionRequested,
                Comments = comments,
                CreatedAt = now
            };

            _context.ApprovalDecisions.Add(failedDecision);

            await _context.SaveChangesAsync();

            return workflow;
        }

        workflow.RevisionCount += 1;
        workflow.Status = WorkflowStatuses.RevisionRequested;
        workflow.UpdatedAt = now;

        var decision = new ApprovalDecision
        {
            WorkflowId = workflow.Id,
            DecidedByUserId = decidedByUserId,
            Decision = ApprovalDecisions.RevisionRequested,
            Comments = comments,
            CreatedAt = now
        };

        _context.ApprovalDecisions.Add(decision);

        await _context.SaveChangesAsync();

        return workflow;
    }

    private static void EnsureDecisionAllowed(
        AgentWorkflow workflow)
    {
        var decisionAllowed =
            workflow.Status == WorkflowStatuses.Planning ||
            workflow.Status == WorkflowStatuses.AwaitingApproval;

        if (!decisionAllowed)
        {
            throw new InvalidOperationException(
                $"Workflow '{workflow.Id}' cannot receive another " +
                $"approval decision while its status is " +
                $"'{workflow.Status}'.");
        }
    }
}