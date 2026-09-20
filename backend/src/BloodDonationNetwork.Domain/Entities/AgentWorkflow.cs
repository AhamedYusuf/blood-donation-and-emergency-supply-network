using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Domain.Entities;

public class AgentWorkflow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BloodRequestId { get; set; }

    public string Status { get; set; } =
        WorkflowStatuses.Planning;

    // Human-readable description of what this workflow
    // is trying to achieve.
    public string Objective { get; set; } = string.Empty;

    // Agent currently responsible for the active workflow step.
    // Null after the workflow reaches a terminal state.
    public string? CurrentAgent { get; set; } =
        AgentNames.Coordinator;

    public int RevisionCount { get; set; } = 0;

    public DateTime StartedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? FailureReason { get; set; }

    // Navigation
    public BloodRequest? BloodRequest { get; set; }

    public ICollection<AgentStep> Steps { get; set; } =
        new List<AgentStep>();

    public ICollection<ApprovalDecision> ApprovalDecisions { get; set; } =
        new List<ApprovalDecision>();
}