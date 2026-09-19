using System.ComponentModel.DataAnnotations;

namespace BloodDonationNetwork.Application.DTOs.Organizations;

public class CreateOrganizationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Address { get; set; } = string.Empty;


    [Required]
    [Phone]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;
}
