namespace BloodDonationNetwork.Application.DTOs.Notifications;

public sealed record DonorNotificationDto(
    Guid Id,
    string Title,
    string Body,
    Dictionary<string, string>? Data,
    DateTime CreatedAt,
    DateTime? ReadAt);
