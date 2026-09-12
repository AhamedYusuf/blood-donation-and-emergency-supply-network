using BloodDonationNetwork.Application.DTOs.Auth;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IStaffInvitationService
{
    /// <summary>Admin-only. Throws <see cref="KeyNotFoundException"/> if the
    /// organization doesn't exist, <see cref="InvalidOperationException"/>
    /// if the email already has an account or an unexpired pending
    /// invite.</summary>
    Task<StaffInvitationResponse> CreateAsync(Guid invitedByAdminId, CreateStaffInvitationRequest request);

    /// <summary>Public. Throws <see cref="InvalidOperationException"/> if
    /// the token is missing, already used, or expired.</summary>
    Task<AuthResponse> AcceptAsync(AcceptStaffInvitationRequest request);

    /// <summary>Admin-only: invitations for one organization (pending and
    /// past), newest first.</summary>
    Task<List<StaffInvitationResponse>> GetByOrganizationAsync(Guid organizationId);
}
