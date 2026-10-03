using BloodDonationNetwork.Application.DTOs.Auth;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IAuthService
{
    Task<CurrentUserResponse?> GetCurrentUserAsync(Guid userId);

    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    Task<AuthResponse> LoginAsync(LoginRequest request);

    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
}