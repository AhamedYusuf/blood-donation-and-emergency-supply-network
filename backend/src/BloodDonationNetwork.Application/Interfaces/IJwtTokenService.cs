namespace BloodDonationNetwork.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Guid userId, string email, string role, Guid? organizationId);

    string GenerateRefreshToken();
}