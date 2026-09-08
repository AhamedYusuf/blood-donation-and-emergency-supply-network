using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Application.Interfaces;

namespace BloodDonationNetwork.Application.Services;

public class EligibilityRuleEngine : IEligibilityRuleEngine
{
    public (bool IsEligible, string? Reason) Evaluate(DonorProfile donor, string requiredBloodType)
    {
        if (!donor.VerifiedByAdmin) return (false, "verified_by_admin = false");

        if (donor.LastDonationDate is { } last)
        {
            var daysSinceLastDonation = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - last.DayNumber;
            if (daysSinceLastDonation < 90)
                return (false, $"last_donation_date within 90 days ({90 - daysSinceLastDonation} days remaining)");
        }

        var age = DateTime.UtcNow.Year - donor.DateOfBirth.Year;
        if (age < 18 || age > 65) return (false, $"age {age} outside 18-65");

        if (donor.MedicalFlags.TryGetValue("recent_illness", out var illness) && illness)
            return (false, "medical_flags.recent_illness = true");

        if (donor.BloodType != requiredBloodType) return (false, "blood type mismatch");

        return (true, null);
    }
}