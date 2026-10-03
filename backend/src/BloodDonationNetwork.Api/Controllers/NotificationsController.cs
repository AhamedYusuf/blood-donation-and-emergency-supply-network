using System.Security.Claims;
using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Notifications;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.Api.Controllers;

/// <summary>
/// A donor's own notification inbox — the durable record NotificationService
/// writes alongside every push attempt, independent of whether the push
/// actually delivered. Distinct from DevicesController, which only manages
/// FCM device-token registration.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "donor")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>The caller's own notifications, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<DonorNotificationDto>>> GetAll(
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _notifications.GetForDonorAsync(CurrentUserId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Count of the caller's own unread notifications — for a badge.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var count = await _notifications.GetUnreadCountAsync(CurrentUserId, ct);
        return Ok(count);
    }

    /// <summary>Marks one of the caller's own notifications as read.</summary>
    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        try
        {
            await _notifications.MarkAsReadAsync(id, CurrentUserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
