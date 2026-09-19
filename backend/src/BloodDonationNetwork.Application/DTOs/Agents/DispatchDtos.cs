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

// Shape matches Tech Doc §4.4 Mode 2 output exactly: notifiedDonorIds and
// appointmentsCreated as ID arrays, failedNotifications as {donorId, reason}.
public class DispatchResponseDto
{
    public Guid WorkflowId { get; set; }
    public List<Guid> NotifiedDonorIds { get; set; } = new();
    public List<Guid> AppointmentsCreated { get; set; } = new();
    public List<FailedNotificationDto> FailedNotifications { get; set; } = new();
}

public class FailedNotificationDto
{
    public Guid DonorId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
