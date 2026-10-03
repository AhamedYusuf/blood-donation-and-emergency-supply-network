namespace BloodDonationNetwork.Application.DTOs.Auth;

public class CurrentUserResponse
{
    public Guid UserId { get; set; }

    public string Role { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }
}
