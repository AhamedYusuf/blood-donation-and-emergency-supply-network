using BloodDonationNetwork.Domain.Entities;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IEligibilityRuleEngine
{
    (bool IsEligible, string? Reason) Evaluate(DonorProfile donor, string requiredBloodType);
}