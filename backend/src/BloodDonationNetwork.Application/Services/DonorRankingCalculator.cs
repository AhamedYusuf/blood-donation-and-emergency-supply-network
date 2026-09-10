namespace BloodDonationNetwork.Application.Services;

public class DonorRankingCalculator
{
    // Weights per Tech Doc §4.4: score = w1*proximity + w2*urgency + w3*reliability
    private const double ProximityWeight = 0.5;
    private const double UrgencyWeight = 0.2;
    private const double ReliabilityWeight = 0.3;

    public double CalculateScore(double distanceKm, string urgencyLevel, double reliabilityScore)
    {
        var proximityScore = CalculateProximityScore(distanceKm);
        var urgencyBoost = CalculateUrgencyBoost(urgencyLevel, distanceKm);

        return (ProximityWeight * proximityScore)
             + (UrgencyWeight * urgencyBoost)
             + (ReliabilityWeight * reliabilityScore);
    }

    // Closer = higher score. Caps at 0 for anything beyond 50km so
    // score never goes negative.
    private static double CalculateProximityScore(double distanceKm)
    {
        const double maxRelevantDistance = 50.0;
        var score = 1.0 - (distanceKm / maxRelevantDistance);
        return Math.Max(0, score);
    }

    // Critical requests get a boost that effectively widens tolerance for
    // distance — a far-but-available donor still ranks reasonably for a
    // critical request, per §4.4's "widen tolerance for distance" note.
    private static double CalculateUrgencyBoost(string urgencyLevel, double distanceKm)
    {
        return urgencyLevel switch
        {
            "critical" => 1.0,
            "urgent" => 0.6,
            "routine" => 0.2,
            _ => throw new ArgumentException($"Invalid urgency level: '{urgencyLevel}'")
        };
    }
}