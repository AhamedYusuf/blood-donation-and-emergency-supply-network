using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;

namespace BloodDonationNetwork.Application.Services;

public class EligibilityRuleEngine : IEligibilityRuleEngine
{
    /// <param name="requiredBloodType">The RECIPIENT's (patient's) blood type — the one
    /// the request is for. See BloodCompatibility for the donor→recipient direction.</param>
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

        IReadOnlyList<string> compatibleDonorTypes;
        try
        {
            compatibleDonorTypes = BloodCompatibility.GetCompatibleDonorTypes(requiredBloodType);
        }
        catch (ArgumentException)
        {
            // Fail closed at THIS boundary rather than let one bad record crash the batch —
            // the shared utility is allowed to throw; the safety gate isn't.
            return (false, $"unrecognized recipient blood type '{requiredBloodType}'");
        }

        if (!compatibleDonorTypes.Contains(donor.BloodType))
            return (false, $"blood type {donor.BloodType} not compatible with recipient {requiredBloodType}");

        return (true, null);
    }
}