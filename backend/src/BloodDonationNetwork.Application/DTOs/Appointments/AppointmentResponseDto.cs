namespace BloodDonationNetwork.Application.DTOs.Appointments;

public class AppointmentResponseDto
{
    public Guid Id { get; set; }
    public Guid DonorId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? RelatedWorkflowId { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string Status { get; set; } = string.Empty;

    // The donor's blood type (e.g. "O+"), copied from their DonorProfile.
    // Null when the donor has no profile row yet.
    public string? DonorBloodType { get; set; }

    public int? UnitsDonated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}