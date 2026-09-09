using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.DTOs.Requests;

public class RequestResponseDto
{
    public Guid Id { get; set; }

    public Guid RequesterId { get; set; }

    public Guid OrganizationId { get; set; }

    public BloodType BloodType { get; set; }

    public int UnitsRequested { get; set; }

    public RequestUrgency Urgency { get; set; }

    public BloodRequestStatus Status { get; set; }

    public string HospitalName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? FulfilledAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}