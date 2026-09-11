using System.Security.Claims;
using BloodDonationNetwork.Api.Controllers;
using BloodDonationNetwork.Application.DTOs.Notifications;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.UnitTests.Notifications;

// The [Authorize(Roles = "donor")] gate is enforced by the MVC pipeline;
// these cover the in-method behaviour: validation, that the caller's own
// user id is used, and result mapping.
public class DevicesControllerTests
{
    private static readonly Guid Caller = Guid.Parse("0d000000-0000-0000-0000-000000000009");

    private sealed class FakeNotificationService : INotificationService
    {
        public (Guid donor, string token, string platform)? Registered { get; private set; }
        public (Guid donor, string token)? Unregistered { get; private set; }
        public Guid? SentTo { get; private set; }
        public string? SentTitle { get; private set; }

        public Task RegisterDeviceAsync(Guid donorUserId, string fcmToken, string platform, CancellationToken ct = default)
        {
            Registered = (donorUserId, fcmToken, platform);
            return Task.CompletedTask;
        }

        public Task UnregisterDeviceAsync(Guid donorUserId, string fcmToken, CancellationToken ct = default)
        {
            Unregistered = (donorUserId, fcmToken);
            return Task.CompletedTask;
        }

        public Task<NotificationResult> SendToDonorAsync(
            Guid donorUserId, string title, string body,
            IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
        {
            SentTo = donorUserId;
            SentTitle = title;
            return Task.FromResult(new NotificationResult(donorUserId, 1, 1, 0, true));
        }
    }

    private static DevicesController Build(INotificationService svc)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Caller.ToString()), new Claim(ClaimTypes.Role, "donor")],
            "Test");
        return new DevicesController(svc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    [Fact]
    public async Task Register_BlankToken_Returns400()
    {
        var result = await Build(new FakeNotificationService())
            .Register(new RegisterDeviceRequest { FcmToken = "  " }, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_ForwardsCallerIdTokenAndPlatform_Returns204()
    {
        var svc = new FakeNotificationService();

        var result = await Build(svc)
            .Register(new RegisterDeviceRequest { FcmToken = "tok-abc", Platform = "ios" }, default);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal((Caller, "tok-abc", "ios"), svc.Registered);
    }

    [Fact]
    public async Task Unregister_BlankToken_Returns400()
    {
        var result = await Build(new FakeNotificationService())
            .Unregister(new UnregisterDeviceRequest { FcmToken = "" }, default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Unregister_ForwardsCallerIdAndToken_Returns204()
    {
        var svc = new FakeNotificationService();

        var result = await Build(svc)
            .Unregister(new UnregisterDeviceRequest { FcmToken = "tok-xyz" }, default);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal((Caller, "tok-xyz"), svc.Unregistered);
    }

    [Fact]
    public async Task SendTest_NotifiesTheCallersOwnDevices()
    {
        var svc = new FakeNotificationService();

        var result = await Build(svc).SendTest(default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<NotificationResult>(ok.Value);
        Assert.Equal(Caller, svc.SentTo);
        Assert.Equal("Test alert", svc.SentTitle);
        Assert.True(payload.AnyDelivered);
    }
}
