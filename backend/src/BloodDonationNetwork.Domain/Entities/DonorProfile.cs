namespace BloodDonationNetwork.Domain.Entities;

public class DonorProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string BloodType { get; set; } = default!;
    public string EligibilityStatus { get; set; } = "pending_review";
    public DateOnly DateOfBirth { get; set; }
    public DateOnly? LastDonationDate { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool LocationVerified { get; set; } = false;
    public Dictionary<string, bool> MedicalFlags { get; set; } = new();
    public bool VerifiedByAdmin { get; set; } = false;
    public decimal ReliabilityScore { get; set; } = 1.0m;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}