using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BloodDonationNetwork.Application.Serialization;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.DTOs.Requests;

public class CreateRequestDto : IValidatableObject
{
    // RequesterId is intentionally not here.
    // The requester is always taken from the authenticated JWT user.
    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    [JsonConverter(typeof(BloodTypeJsonConverter))]
    public BloodType BloodType { get; set; }

    [Range(1, 100)]
    public int UnitsRequested { get; set; }

    [Required]
    [JsonConverter(typeof(RequestUrgencyJsonConverter))]
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

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Latitude == 0 && Longitude == 0)
        {
            yield return new ValidationResult(
                "Hospital location must be provided.",
                new[]
                {
                    nameof(Latitude),
                    nameof(Longitude)
                });
        }
    }
}