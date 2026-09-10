using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Domain.Entities;

namespace BloodDonationNetwork.UnitTests.Appointments;

// Covers the Tech Doc §4.7 requirement for AppointmentService's status
// mapping round-trip, with explicit focus on the NoShow <-> "no_show" case
// that a naive .ToString().ToLowerInvariant() ("noshow") would break.
public class AppointmentStatusMapTests
{
    public static IEnumerable<object[]> AllPairs =>
    [
        [AppointmentStatus.Scheduled, "scheduled"],
        [AppointmentStatus.Completed, "completed"],
        [AppointmentStatus.NoShow, "no_show"],
        [AppointmentStatus.Cancelled, "cancelled"],
    ];

    // ---------------------------------------------------------------
    // ToApiString
    // ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void ToApiString_ReturnsExactContractString(AppointmentStatus status, string expected)
    {
        Assert.Equal(expected, AppointmentStatusMap.ToApiString(status));
    }

    [Fact]
    public void ToApiString_NoShow_IsSnakeCase_NotNaiveLowercase()
    {
        var result = AppointmentStatusMap.ToApiString(AppointmentStatus.NoShow);

        Assert.Equal("no_show", result);
        Assert.NotEqual("noshow", result);
    }

    [Fact]
    public void ToApiString_UndefinedEnumValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AppointmentStatusMap.ToApiString((AppointmentStatus)999));
    }

    // ---------------------------------------------------------------
    // Parse
    // ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Parse_AcceptsExactContractString(AppointmentStatus expected, string input)
    {
        Assert.Equal(expected, AppointmentStatusMap.Parse(input));
    }

    [Theory]
    [InlineData("noshow")]      // the naive .ToLower() form — must be rejected
    [InlineData("NoShow")]      // enum name
    [InlineData("no-show")]     // kebab instead of snake
    [InlineData("Scheduled")]   // wrong casing
    [InlineData("SCHEDULED")]
    [InlineData(" scheduled")]  // leading whitespace — not trimmed
    [InlineData("scheduled ")]  // trailing whitespace
    [InlineData("pending")]     // not an appointment status at all
    [InlineData("")]
    public void Parse_RejectsAnythingButTheExactContractString(string input)
    {
        Assert.Throws<ArgumentException>(() => AppointmentStatusMap.Parse(input));
    }

    // ---------------------------------------------------------------
    // Round-trips
    // ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Parse_Then_ToApiString_ReturnsOriginalString(AppointmentStatus _, string apiString)
    {
        Assert.Equal(apiString, AppointmentStatusMap.ToApiString(AppointmentStatusMap.Parse(apiString)));
    }

    [Fact]
    public void ToApiString_Then_Parse_ReturnsOriginalEnum_ForEveryDefinedValue()
    {
        foreach (AppointmentStatus status in Enum.GetValues<AppointmentStatus>())
        {
            var roundTripped = AppointmentStatusMap.Parse(AppointmentStatusMap.ToApiString(status));
            Assert.Equal(status, roundTripped);
        }
    }
}
