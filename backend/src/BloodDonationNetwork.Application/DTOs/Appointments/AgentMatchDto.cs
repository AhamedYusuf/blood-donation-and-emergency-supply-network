namespace BloodDonationNetwork.Application.DTOs.Appointments;

// Read back from the Matching & Dispatch Agent's own "search_donors"
// agent_steps row for this appointment's workflow — same rank/distance/
// reliability the agent actually used to pick this donor, not
// recomputed. Null when the appointment wasn't agent-created, or the
// step data isn't available (e.g. logged before this donor's profile
// existed).
public class AgentMatchDto
{
    public int Rank { get; set; }
    public double DistanceKm { get; set; }
    public decimal ReliabilityScore { get; set; }
}
