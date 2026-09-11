namespace BloodDonationNetwork.Application.DTOs.Notifications;

public sealed class RegisterDeviceRequest
{
    public string FcmToken { get; set; } = string.Empty;

    /// <summary>"android" | "ios" | "web". Defaults to "android" if blank/unknown.</summary>
    public string Platform { get; set; } = "android";
}

public sealed class UnregisterDeviceRequest
{
    public string FcmToken { get; set; } = string.Empty;
}
