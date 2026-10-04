using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Notifications;
using BloodDonationNetwork.Application.Interfaces;

namespace BloodDonationNetwork.IntegrationTests.TestSupport;

internal sealed class NoOpNotificationService : INotificationService
{
    public Task<NotificationResult> SendToDonorAsync(
        Guid donorUserId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(NotificationResult.NoDevices(donorUserId));

    public Task RegisterDeviceAsync(
        Guid donorUserId,
        string fcmToken,
        string platform,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UnregisterDeviceAsync(
        Guid donorUserId,
        string fcmToken,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<PagedResult<DonorNotificationDto>> GetForDonorAsync(
        Guid donorUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<DonorNotificationDto>());

    public Task<int> GetUnreadCountAsync(
        Guid donorUserId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task MarkAsReadAsync(
        Guid id,
        Guid donorUserId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
