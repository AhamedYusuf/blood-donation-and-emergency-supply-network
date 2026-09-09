using System.ComponentModel.DataAnnotations;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.DTOs.Requests;

public class RequestStatusUpdateDto
{
    [Required]
    public BloodRequestStatus Status { get; set; }
}