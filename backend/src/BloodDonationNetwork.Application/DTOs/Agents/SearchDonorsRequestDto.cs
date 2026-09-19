namespace BloodDonationNetwork.Application.DTOs.Agents;

// Shape matches Tech Doc §4.4 Mode 1 exactly: workflowId, bloodType,
// unitsNeeded, location {lat, lng}, urgencyLevel, radiusKm.
public class SearchDonorsRequestDto
{
    public Guid WorkflowId { get; set; }
    public string BloodType { get; set; } = string.Empty;
    public int UnitsNeeded { get; set; }
    public LocationDto Location { get; set; } = new();
    public string UrgencyLevel { get; set; } = string.Empty;
    public double RadiusKm { get; set; }
}

public class LocationDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}
