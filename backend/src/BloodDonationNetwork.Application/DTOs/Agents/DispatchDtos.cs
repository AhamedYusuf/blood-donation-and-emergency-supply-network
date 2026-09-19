namespace BloodDonationNetwork.Application.DTOs.Agents;

/// <summary>Mode 2 request — payload shape matches what
/// matching_dispatch_agent.py's dispatch() already sends: a workflow id
/// plus the eligible-candidate list the Eligibility Validation Agent
/// produced by filtering Mode 1's own SearchDonorsResponseDto.Candidates,
/// so DonorId here is a DonorProfile.Id, same as DonorCandidateDto.</summary>
public class DispatchRequestDto
{
    public Guid WorkflowId { get; set; }
    public List<DispatchCandidateDto> Eligible { get; set; } = new();
}

public class DispatchCandidateDto
{
    public Guid DonorId { get; set; }
}

public class DispatchResponseDto
{
    public Guid WorkflowId { get; set; }
    public int DonorsContacted { get; set; }
    public int AppointmentsCreated { get; set; }
    public int NotificationsDelivered { get; set; }
    public List<DispatchDonorResultDto> Results { get; set; } = new();
}

public class DispatchDonorResultDto
{
    public Guid DonorId { get; set; }
    public Guid? AppointmentId { get; set; }
    public bool NotificationDelivered { get; set; }
    public string? FailureReason { get; set; }
}
