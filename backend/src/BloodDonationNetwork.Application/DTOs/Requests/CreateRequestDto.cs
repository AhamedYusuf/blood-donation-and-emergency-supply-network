using System.ComponentModel.DataAnnotations;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.DTOs.Requests;

public class CreateRequestDto
{
    // RequesterId is intentionally not here — the requester is always the
    // authenticated caller (RequestsController derives it from the JWT).
    // A client-supplied requester id would let anyone file a request
    // "as" someone else.
    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    public BloodType BloodType { get; set; }

    [Range(1, 100)]
    public int UnitsRequested { get; set; }

    [Required]
    public RequestUrgency Urgency { get; set; }

    [Required]
    [MaxLength(200)]
    public string HospitalName { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;
}