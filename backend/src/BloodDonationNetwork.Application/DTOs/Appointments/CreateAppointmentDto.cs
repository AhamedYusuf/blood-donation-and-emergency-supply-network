namespace BloodDonationNetwork.Application.DTOs.Appointments;

public class CreateAppointmentDto
{
    public Guid OrganizationId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public Guid? RelatedWorkflowId { get; set; }
}