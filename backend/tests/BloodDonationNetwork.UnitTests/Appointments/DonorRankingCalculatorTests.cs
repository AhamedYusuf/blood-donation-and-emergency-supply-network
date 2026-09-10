using BloodDonationNetwork.Application.Services;

namespace BloodDonationNetwork.UnitTests.Appointments;

// Covers Tech Doc §4.7: "Unit tests for the ranking/scoring function
// (weights, tie-breaking, urgency boost)."
//
// The scoring formula under test (Tech Doc §4.4):
//   score = 0.5 * proximity + 0.2 * urgencyBoost + 0.3 * reliability
// where
//   proximity     = max(0, 1 - distanceKm / 50)   (clamped, never negative)
//   urgencyBoost   = critical -> 1.0, urgent -> 0.6, routine -> 0.2
//
// Note: the weight values (0.5 / 0.2 / 0.3) are a Student-4 assumption,
// not fixed by the Tech Doc — these tests pin the *current* behaviour so a
// later weight change is a deliberate, visible edit, not a silent drift.
public class DonorRankingCalculatorTests
{
    private const int Precision = 10;

    private readonly DonorRankingCalculator _calculator = new();

    // ---------------------------------------------------------------
    // Full-formula values — exact expected scores for known inputs
    // ---------------------------------------------------------------

    [Theory]
    // distanceKm, urgency, reliability, expectedScore
    [InlineData(0.0, "critical", 1.0, 1.0)]     // 0.5*1   + 0.2*1.0 + 0.3*1   = 1.00  (max)
    [InlineData(0.0, "critical", 0.0, 0.7)]     // 0.5*1   + 0.2*1.0 + 0.3*0   = 0.70
    [InlineData(25.0, "urgent", 0.5, 0.52)]     // 0.5*0.5 + 0.2*0.6 + 0.3*0.5 = 0.52
    [InlineData(50.0, "routine", 0.0, 0.04)]    // 0.5*0   + 0.2*0.2 + 0.3*0   = 0.04
    [InlineData(12.5, "routine", 1.0, 0.715)]   // 0.5*0.75+ 0.2*0.2 + 0.3*1   = 0.715
    public void CalculateScore_ReturnsExactWeightedSum(
        double distanceKm, string urgency, double reliability, double expected)
    {
        var score = _calculator.CalculateScore(distanceKm, urgency, reliability);

        Assert.Equal(expected, score, Precision);
    }

    // ---------------------------------------------------------------
    // Proximity component — closer is better, clamped at 0
    // ---------------------------------------------------------------

    [Fact]
    public void CalculateScore_ZeroDistance_GivesFullProximityContribution()
    {
        // Only the proximity term is non-zero here (routine boost still adds 0.04).
        var score = _calculator.CalculateScore(0.0, "routine", 0.0);

        Assert.Equal(0.5 + 0.04, score, Precision);
    }

    [Fact]
    public void CalculateScore_DistanceBeyond50km_ClampsProximityToZero_NeverNegative()
    {
        var atCap = _calculator.CalculateScore(50.0, "routine", 0.0);
        var farBeyond = _calculator.CalculateScore(500.0, "routine", 0.0);

        Assert.Equal(0.04, atCap, Precision);        // proximity == 0
        Assert.Equal(atCap, farBeyond, Precision);   // still 0, not negative
        Assert.True(farBeyond >= 0.0);
    }

    [Fact]
    public void CalculateScore_CloserDonor_ScoresHigher_WhenUrgencyAndReliabilityEqual()
    {
        var near = _calculator.CalculateScore(2.0, "urgent", 0.8);
        var far = _calculator.CalculateScore(40.0, "urgent", 0.8);

        Assert.True(near > far);
    }

    // ---------------------------------------------------------------
    // Urgency boost
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("critical", 0.2)]   // 0.2 weight * 1.0 boost
    [InlineData("urgent", 0.12)]    // 0.2 weight * 0.6 boost
    [InlineData("routine", 0.04)]   // 0.2 weight * 0.2 boost
    public void CalculateScore_UrgencyBoostContribution_IsIsolatedAndCorrect(
        string urgency, double expectedBoostContribution)
    {
        // distance beyond cap => proximity term 0; reliability 0 => that term 0.
        // Whatever remains is purely the urgency contribution.
        var score = _calculator.CalculateScore(100.0, urgency, 0.0);

        Assert.Equal(expectedBoostContribution, score, Precision);
    }

    [Fact]
    public void CalculateScore_CriticalOutranksUrgentOutranksRoutine_AllElseEqual()
    {
        var critical = _calculator.CalculateScore(10.0, "critical", 0.5);
        var urgent = _calculator.CalculateScore(10.0, "urgent", 0.5);
        var routine = _calculator.CalculateScore(10.0, "routine", 0.5);

        Assert.True(critical > urgent);
        Assert.True(urgent > routine);
    }

    [Theory]
    [InlineData("Critical")]   // wrong casing
    [InlineData("URGENT")]
    [InlineData("normal")]     // the C# RequestUrgency enum name, NOT the §0.5 spec value
    [InlineData("")]
    [InlineData("emergency")]
    public void CalculateScore_UnrecognisedUrgency_Throws(string urgency)
    {
        Assert.Throws<ArgumentException>(
            () => _calculator.CalculateScore(5.0, urgency, 0.9));
    }

    // ---------------------------------------------------------------
    // Reliability component
    // ---------------------------------------------------------------

    [Fact]
    public void CalculateScore_HigherReliability_ScoresStrictlyHigher_AllElseEqual()
    {
        var low = _calculator.CalculateScore(10.0, "urgent", 0.10);
        var mid = _calculator.CalculateScore(10.0, "urgent", 0.55);
        var high = _calculator.CalculateScore(10.0, "urgent", 0.95);

        Assert.True(low < mid);
        Assert.True(mid < high);
    }

    [Fact]
    public void CalculateScore_ReliabilityContribution_IsThirtyPercentOfTheValue()
    {
        // Isolate reliability: proximity clamped to 0, routine boost is a known 0.04.
        var withZero = _calculator.CalculateScore(100.0, "routine", 0.0);
        var withOne = _calculator.CalculateScore(100.0, "routine", 1.0);

        Assert.Equal(0.3, withOne - withZero, Precision);
    }

    // ---------------------------------------------------------------
    // Determinism (basis for the ranking layer's tie handling)
    // ---------------------------------------------------------------

    [Fact]
    public void CalculateScore_IsDeterministic_SameInputsGiveIdenticalScore()
    {
        var a = _calculator.CalculateScore(17.3, "critical", 0.42);
        var b = _calculator.CalculateScore(17.3, "critical", 0.42);

        Assert.Equal(a, b);   // exact equality — no tolerance
    }

    [Fact]
    public void CalculateScore_DifferentInputsTunedToTheSameScore_ProduceEqualScores()
    {
        // A donor 0 km away with reliability 0.0, and a donor 6 km away with
        // reliability 0.2, both "routine": proximity/reliability trade off to
        // the same total. The calculator itself provides no tie-break — that is
        // the ranking layer's responsibility (MatchingDispatchAgentService).
        //   0 km:  0.5*1.00 + 0.04 + 0.3*0.0 = 0.54
        //   6 km:  0.5*0.88 + 0.04 + 0.3*0.2 = 0.54
        var donorA = _calculator.CalculateScore(0.0, "routine", 0.0);
        var donorB = _calculator.CalculateScore(6.0, "routine", 0.2);

        Assert.Equal(0.54, donorA, Precision);
        Assert.Equal(donorA, donorB, Precision);
    }
}
