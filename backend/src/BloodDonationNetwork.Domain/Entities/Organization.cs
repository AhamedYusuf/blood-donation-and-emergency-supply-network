namespace BloodDonationNetwork.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public OrganizationType Type { get; set; }

    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}