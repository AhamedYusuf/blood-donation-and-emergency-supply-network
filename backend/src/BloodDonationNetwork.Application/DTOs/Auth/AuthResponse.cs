namespace BloodDonationNetwork.Application.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    // The organization the user belongs to (staff). Null for donors and
    // admins with no organization. Also carried as the "organizationId"
    // JWT claim (Tech Doc §0.4).
    public Guid? OrganizationId { get; set; }
}