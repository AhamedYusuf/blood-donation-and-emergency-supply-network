using BloodDonationNetwork.Application.Common;
using Xunit;

namespace BloodDonationNetwork.UnitTests.Appointments;

public class GeoUtilsTests
{
    [Fact]
    public void London_to_Paris_is_about_344_kilometres()
    {
        var distance = GeoUtils.DistanceKm(51.5074, -0.1278, 48.8566, 2.3522);

        Assert.InRange(distance, 341, 346);
    }

    [Fact]
    public void New_York_to_Los_Angeles_is_about_3936_kilometres()
    {
        var distance = GeoUtils.DistanceKm(40.7128, -74.0060, 34.0522, -118.2437);

        Assert.InRange(distance, 3930, 3942);
    }
}
