using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using Xunit;

public class EligibilityRuleEngineTests
{
    private readonly EligibilityRuleEngine _engine = new();

    private static DonorProfile MakeDonor(bool verified = true, DateOnly? lastDonation = null,
        int age = 30, bool illness = false, string bloodType = "O-") => new()
    {
        VerifiedByAdmin = verified,
        LastDonationDate = lastDonation,
        DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-age)),
        MedicalFlags = new() { ["recent_illness"] = illness },
        BloodType = bloodType
    };

    [Fact]
    public void Fails_when_not_verified()
    {
        var (isEligible, reason) = _engine.Evaluate(MakeDonor(verified: false), "O-");
        Assert.False(isEligible);
        Assert.Contains("verified_by_admin", reason);
    }

    [Fact]
    public void Fails_within_90_days()
    {
        var donor = MakeDonor(lastDonation: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        var (isEligible, _) = _engine.Evaluate(donor, "O-");
        Assert.False(isEligible);
    }

    [Fact]
    public void Passes_at_exactly_90_days()
    {
        var donor = MakeDonor(lastDonation: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90)));
        var (isEligible, _) = _engine.Evaluate(donor, "O-");
        Assert.True(isEligible);
    }

    [Theory]
    [InlineData(17, false)]
    [InlineData(18, true)]
    [InlineData(65, true)]
    [InlineData(66, false)]
    public void Age_boundaries(int age, bool expected)
    {
        var (isEligible, _) = _engine.Evaluate(MakeDonor(age: age), "O-");
        Assert.Equal(expected, isEligible);
    }

    [Fact]
    public void Fails_on_illness_flag()
    {
        var (isEligible, _) = _engine.Evaluate(MakeDonor(illness: true), "O-");
        Assert.False(isEligible);
    }

    [Fact]
    public void Fails_on_type_mismatch()
    {
        var (isEligible, _) = _engine.Evaluate(MakeDonor(bloodType: "A+"), "O-");
        Assert.False(isEligible);
    }

    [Fact]
    public void Passes_when_all_rules_ok()
    {
        var (isEligible, reason) = _engine.Evaluate(MakeDonor(), "O-");
        Assert.True(isEligible);
        Assert.Null(reason);
    }
}