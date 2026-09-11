using System.Security.Claims;
using BloodDonationNetwork.Application.DTOs.Notifications;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

/// <summary>
/// A donor's push-notification devices. The Flutter app registers its FCM
/// token here on login and removes it on logout. <c>POST /test</c> sends a
/// notification to the caller's own devices — a way to prove the FCM
/// integration end-to-end without the full agent dispatch pipeline.
/// </summary>
[ApiController]
[Route("api/devices")]
[Authorize(Roles = "donor")]
public class DevicesController : ControllerBase
{
    private readonly INotificationService _notifications;

    public DevicesController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Register / refresh this device's FCM token.</summary>
    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FcmToken))
        {
            return BadRequest(new { error = "fcmToken is required." });
        }

        await _notifications.RegisterDeviceAsync(
            CurrentUserId, request.FcmToken, request.Platform, ct);
        return NoContent();
    }

    /// <summary>Remove this device's FCM token (called on logout).</summary>
    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister(UnregisterDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FcmToken))
        {
            return BadRequest(new { error = "fcmToken is required." });
        }

        await _notifications.UnregisterDeviceAsync(CurrentUserId, request.FcmToken, ct);
        return NoContent();
    }

    /// <summary>Send a test push to the caller's own devices.</summary>
    [HttpPost("test")]
    public async Task<ActionResult<NotificationResult>> SendTest(CancellationToken ct)
    {
        var result = await _notifications.SendToDonorAsync(
            CurrentUserId,
            title: "Test alert",
            body: "This is a test notification from the Blood Donation Network.",
            data: new Dictionary<string, string> { ["type"] = "test" },
            cancellationToken: ct);

        return Ok(result);
    }
}
