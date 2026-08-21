namespace BloodDonationNetwork.Application.DTOs.Appointments;

public class AppointmentResponseDto
{
    public Guid Id { get; set; }
    public Guid DonorId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? RelatedWorkflowId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? UnitsDonated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}