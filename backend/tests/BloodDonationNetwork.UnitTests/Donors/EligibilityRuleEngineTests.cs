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

    [Theory]
    [InlineData("O-", "AB+")] [InlineData("O-", "O-")] [InlineData("O-", "B+")]
    public void Universal_donor_is_compatible_with_everyone(string donor, string recipient)
        => Assert.True(_engine.Evaluate(MakeDonor(bloodType: donor), recipient).IsEligible);

    [Theory]
    [InlineData("AB+", "AB+")] [InlineData("O+", "AB+")] [InlineData("B-", "AB+")]
    public void Universal_recipient_accepts_everyone(string donor, string recipient)
        => Assert.True(_engine.Evaluate(MakeDonor(bloodType: donor), recipient).IsEligible);

    [Theory]
    [InlineData("O+", "O-")]   // Rh mismatch — catches "O is universal" oversimplification
    [InlineData("A+", "A-")]
    [InlineData("AB+", "A+")]
    public void Incompatible_pairs_are_rejected(string donor, string recipient)
        => Assert.False(_engine.Evaluate(MakeDonor(bloodType: donor), recipient).IsEligible);

    [Fact]
    public void Unknown_recipient_type_fails_closed()
        => Assert.False(_engine.Evaluate(MakeDonor(bloodType: "O-"), "XYZ").IsEligible);

    [Fact]
    public void Unknown_recipient_type_fails_closed_without_throwing()
    {
        var (ok, reason) = _engine.Evaluate(MakeDonor(bloodType: "O-"), "XYZ");
        Assert.False(ok);
        Assert.Contains("unrecognized", reason, StringComparison.OrdinalIgnoreCase);
    }
}