using BloodDonationNetwork.Application.Common;
using Xunit;

namespace BloodDonationNetwork.UnitTests.Appointments;

public class BloodCompatibilityTests
{
    [Theory]
    [InlineData("O-", new[] { "O-" })]
    [InlineData("O+", new[] { "O-", "O+" })]
    [InlineData("A-", new[] { "O-", "A-" })]
    [InlineData("A+", new[] { "O-", "O+", "A-", "A+" })]
    [InlineData("B-", new[] { "O-", "B-" })]
    [InlineData("B+", new[] { "O-", "O+", "B-", "B+" })]
    [InlineData("AB-", new[] { "O-", "A-", "B-", "AB-" })]
    [InlineData("AB+", new[] { "O-", "O+", "A-", "A+", "B-", "B+", "AB-", "AB+" })]
    public void Returns_the_correct_compatible_donor_set_for_every_recipient_type(
        string recipientBloodType, string[] expectedDonorTypes)
    {
        var result = BloodCompatibility.GetCompatibleDonorTypes(recipientBloodType);

        Assert.Equal(expectedDonorTypes.OrderBy(t => t), result.OrderBy(t => t));
    }

    [Fact]
    public void O_negative_is_universal_and_appears_in_every_compatible_set()
    {
        foreach (var recipientType in new[] { "O-", "O+", "A-", "A+", "B-", "B+", "AB-", "AB+" })
        {
            Assert.Contains("O-", BloodCompatibility.GetCompatibleDonorTypes(recipientType));
        }
    }

    [Fact]
    public void AB_positive_recipients_can_accept_from_every_donor_type()
    {
        var result = BloodCompatibility.GetCompatibleDonorTypes("AB+");

        Assert.Equal(8, result.Count);
    }

    [Fact]
    public void O_negative_recipients_can_only_accept_from_O_negative_donors()
    {
        var result = BloodCompatibility.GetCompatibleDonorTypes("O-");

        Assert.Single(result);
        Assert.Equal("O-", result[0]);
    }

    [Fact]
    public void Throws_for_an_unrecognized_blood_type()
    {
        Assert.Throws<ArgumentException>(() => BloodCompatibility.GetCompatibleDonorTypes("X+"));
    }
}
