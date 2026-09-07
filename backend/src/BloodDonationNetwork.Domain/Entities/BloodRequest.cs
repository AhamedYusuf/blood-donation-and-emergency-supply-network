using System;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Domain.Entities;

public class BloodRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public Guid OrganizationId { get; set; }
    public BloodType BloodType { get; set; }
    public int UnitsRequested { get; set; }
    public RequestUrgency Urgency { get; set; }
    public BloodRequestStatus Status { get; set; } = BloodRequestStatus.Pending;
    public string HospitalName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FulfilledAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Navigation properties
    public User? Requester { get; set; }
    public Organization? Organization { get; set; }
}