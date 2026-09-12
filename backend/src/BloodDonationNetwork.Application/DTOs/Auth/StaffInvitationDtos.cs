using System.ComponentModel.DataAnnotations;

namespace BloodDonationNetwork.Application.DTOs.Auth;

/// <summary>Admin-only: invite someone to become staff at an organization.</summary>
public class CreateStaffInvitationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public Guid OrganizationId { get; set; }
}

public class StaffInvitationResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Share this with the invitee out of band — there's no email
    /// infrastructure in this project. Only ever returned once, at
    /// creation time.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

/// <summary>Public: redeem an invitation token to create the staff account
/// it names. Role and organization come from the invitation record, never
/// from this request — mirrors why donor self-registration ignores those
/// fields too.</summary>
public class AcceptStaffInvitationRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
}
