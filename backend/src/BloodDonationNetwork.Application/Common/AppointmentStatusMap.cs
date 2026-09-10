using BloodDonationNetwork.Domain.Entities;

namespace BloodDonationNetwork.Application.Common;

/// <summary>
/// Converts between the <see cref="AppointmentStatus"/> enum and the exact
/// lowercase / snake_case strings the API contract uses (Tech Doc §0.5:
/// <c>scheduled, completed, no_show, cancelled</c>).
///
/// The mapping is explicit on purpose: <c>NoShow.ToString().ToLowerInvariant()</c>
/// would yield <c>"noshow"</c>, not the required <c>"no_show"</c>.
/// </summary>
public static class AppointmentStatusMap
{
    public static string ToApiString(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Scheduled => "scheduled",
        AppointmentStatus.Completed => "completed",
        AppointmentStatus.NoShow => "no_show",
        AppointmentStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status), status, "Unknown appointment status")
    };

    public static AppointmentStatus Parse(string status) => status switch
    {
        "scheduled" => AppointmentStatus.Scheduled,
        "completed" => AppointmentStatus.Completed,
        "no_show" => AppointmentStatus.NoShow,
        "cancelled" => AppointmentStatus.Cancelled,
        _ => throw new ArgumentException($"Invalid appointment status: '{status}'")
    };
}
