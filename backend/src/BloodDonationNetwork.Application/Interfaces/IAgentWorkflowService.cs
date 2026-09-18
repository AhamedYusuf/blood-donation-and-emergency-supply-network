using BloodDonationNetwork.Domain.Entities;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IAgentWorkflowService
{
    Task<AgentWorkflow?> GetByIdAsync(Guid workflowId);

    Task<AgentWorkflow?> GetLatestByBloodRequestIdAsync(
        Guid bloodRequestId);

    Task<List<AgentStep>> GetStepsAsync(Guid workflowId);

    Task<object?> GetSummaryAsync(Guid workflowId);

    Task<AgentWorkflow?> ApproveAsync(
        Guid workflowId,
        Guid decidedByUserId,
        string? comments);

    Task<AgentWorkflow?> RejectAsync(
        Guid workflowId,
        Guid decidedByUserId,
        string? comments);

    Task<AgentWorkflow?> ReviseAsync(
        Guid workflowId,
        Guid decidedByUserId,
        string? comments);
}