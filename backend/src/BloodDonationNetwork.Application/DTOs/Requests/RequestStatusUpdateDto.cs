using System.ComponentModel.DataAnnotations;

namespace BloodDonationNetwork.Application.DTOs.Requests;

public class RequestStatusUpdateDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}