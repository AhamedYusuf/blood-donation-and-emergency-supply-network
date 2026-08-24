namespace BloodDonationNetwork.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public string? RefreshToken { get; set; }

public DateTime? RefreshTokenExpiryTime { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}