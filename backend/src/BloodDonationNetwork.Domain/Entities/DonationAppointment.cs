namespace BloodDonationNetwork.Domain.Entities;

public class DonationAppointment
{
    public Guid Id { get; set; }

    // FK to donor_profiles.id — Student 1's table doesn't exist yet,
    // so this is a plain Guid column for now, no navigation property,
    // no FK constraint. We'll wire the real relationship once
    // DonorProfile exists and is merged into development.
    public Guid DonorId { get; set; }

    // FK to organizations.id — Organization already exists (Foundation
    // Setup), so this gets a real navigation property + FK constraint.
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    // FK to agent_workflows.id — nullable (an appointment can be booked
    // directly by a donor, not just via a dispatched workflow) AND
    // Student 2's table doesn't exist yet — same placeholder approach.
    public Guid? RelatedWorkflowId { get; set; }

    public DateTime ScheduledTime { get; set; }

    public AppointmentStatus Status { get; set; }

    // Nullable — only filled in once POST /api/appointments/{id}/complete runs
    public int? UnitsDonated { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}