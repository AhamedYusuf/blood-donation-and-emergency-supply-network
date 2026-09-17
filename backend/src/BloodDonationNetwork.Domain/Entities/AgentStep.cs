namespace BloodDonationNetwork.Domain.Entities;

public class AgentStep
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid WorkflowId { get; set; }
	public string AgentName { get; set; } = default!;
	public string InputData { get; set; } = "{}";
	public string? OutputData { get; set; }
	public string Status { get; set; } = "running";
	public DateTime StartedAt { get; set; } = DateTime.UtcNow;
	public DateTime? CompletedAt { get; set; }
	public string? ErrorMessage { get; set; }
	public int RetryCount { get; set; }
}
