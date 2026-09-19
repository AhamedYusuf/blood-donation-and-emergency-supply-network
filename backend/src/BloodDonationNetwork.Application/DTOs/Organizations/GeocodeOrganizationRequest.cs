using System.ComponentModel.DataAnnotations;

namespace BloodDonationNetwork.Application.DTOs.Organizations;

public class GeocodeOrganizationRequest
{
    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Address { get; set; } = string.Empty;
}
