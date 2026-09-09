namespace BloodDonationNetwork.Application.DTOs.Agents;

public class SearchDonorsRequestDto
{
    public string BloodType { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; }
    public string UrgencyLevel { get; set; } = string.Empty;
    public Guid WorkflowId { get; set; }
}