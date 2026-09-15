namespace BloodDonationNetwork.Domain.Entities;

public class ApprovalDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowId { get; set; }

    public Guid DecidedByUserId { get; set; }

    public string Decision { get; set; } = string.Empty;

    public string? Comments { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AgentWorkflow? Workflow { get; set; }
}