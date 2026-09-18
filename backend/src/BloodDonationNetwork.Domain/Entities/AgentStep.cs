namespace BloodDonationNetwork.Domain.Entities;

public class AgentStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowId { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string StepName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public string? Narrative { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    // Navigation
    public AgentWorkflow? Workflow { get; set; }
}