using System.Security.Claims;
using BloodDonationNetwork.Api.Controllers;
using BloodDonationNetwork.Application.DTOs.Appointments;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BloodDonationNetwork.UnitTests.Appointments;

// Covers the Tech Doc §4.7 requirement for the controller-level
// authorization logic that lives *inside* the action methods (ownership
// checks, the donor "cancel-only" rule, and the deliberate order of the
// status-validity check vs. the permission check in UpdateStatus).
//
// Attribute-level role gates ([Authorize(Roles = "...")]) are enforced by
// the MVC pipeline, not by calling the method directly, so those are left
// to integration tests — everything here is in-method branching.
public class AppointmentsControllerAuthorizationTests
{
    private static readonly Guid DonorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DonorB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ApptId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    // ---------------------------------------------------------------
    // GetById
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetById_AppointmentMissing_Returns404()
    {
        var controller = BuildController(new FakeAppointmentService(), DonorA, "donor");

        var result = await controller.GetById(ApptId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetById_OwningDonor_Returns200()
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, DonorA, "donor");

        var result = await controller.GetById(ApptId);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("staff")]
    [InlineData("admin")]
    public async Task GetById_StaffOrAdmin_NonOwner_Returns200(string role)
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, Guid.NewGuid(), role);

        var result = await controller.GetById(ApptId);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_DifferentDonor_Returns403()
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, DonorB, "donor");

        var result = await controller.GetById(ApptId);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // ---------------------------------------------------------------
    // UpdateStatus
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateStatus_AppointmentMissing_Returns404()
    {
        var controller = BuildController(new FakeAppointmentService(), DonorA, "donor");

        var result = await controller.UpdateStatus(ApptId, new UpdateAppointmentStatusDto { NewStatus = "cancelled" });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_NonOwnerDonor_Returns403()
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, DonorB, "donor");

        var result = await controller.UpdateStatus(ApptId, new UpdateAppointmentStatusDto { NewStatus = "cancelled" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_OwnerDonor_Cancelling_Returns200()
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, DonorA, "donor");

        var result = await controller.UpdateStatus(ApptId, new UpdateAppointmentStatusDto { NewStatus = "cancelled" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_OwnerDonor_NonCancelStatus_Returns400_CancelOnlyRule()
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, DonorA, "donor");

        var result = await controller.UpdateStatus(ApptId, new UpdateAppointmentStatusDto { NewStatus = "completed" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("only cancel", bad.Value!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_Staff_MarkingCompleted_Returns200()
    {
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, Guid.NewGuid(), "staff");

        var result = await controller.UpdateStatus(ApptId, new UpdateAppointmentStatusDto { NewStatus = "completed" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatusString_IsRejectedBeforeTheCancelOnlyRule()
    {
        // A donor sending an invalid status must get the "invalid status"
        // message, not the "donors may only cancel" message — the validity
        // check is deliberately ordered first.
        var svc = new FakeAppointmentService { GetByIdResult = AppointmentOwnedBy(DonorA) };
        var controller = BuildController(svc, DonorA, "donor");

        var result = await controller.UpdateStatus(ApptId, new UpdateAppointmentStatusDto { NewStatus = "banana" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid appointment status", bad.Value!.ToString()!);
        Assert.DoesNotContain("only cancel", bad.Value!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    // GetByDonor — self or admin only; staff is explicitly excluded
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetByDonor_Self_Returns200()
    {
        var controller = BuildController(new FakeAppointmentService(), DonorA, "donor");

        var result = await controller.GetByDonor(DonorA);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByDonor_Admin_OtherDonor_Returns200()
    {
        var controller = BuildController(new FakeAppointmentService(), Guid.NewGuid(), "admin");

        var result = await controller.GetByDonor(DonorA);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByDonor_Staff_OtherDonor_Returns403()
    {
        var controller = BuildController(new FakeAppointmentService(), Guid.NewGuid(), "staff");

        var result = await controller.GetByDonor(DonorA);

        Assert.IsType<ForbidResult>(result.Result);
    }

    // ---------------------------------------------------------------
    // GetUpcomingByOrganization — service-thrown UnauthorizedAccess -> 403
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetUpcomingByOrganization_ServiceRejectsOrg_Returns403()
    {
        var svc = new FakeAppointmentService { GetUpcomingThrows = new UnauthorizedAccessException() };
        var controller = BuildController(svc, Guid.NewGuid(), "staff");

        var result = await controller.GetUpcomingByOrganization(Guid.NewGuid());

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetUpcomingByOrganization_Admin_PassesIsAdminTrueToService()
    {
        var svc = new FakeAppointmentService();
        var controller = BuildController(svc, Guid.NewGuid(), "admin");

        await controller.GetUpcomingByOrganization(Guid.NewGuid());

        Assert.True(svc.LastGetUpcomingIsAdmin);
    }

    // ---------------------------------------------------------------
    // Complete — exception mapping + isAdmin passthrough
    // ---------------------------------------------------------------

    [Fact]
    public async Task Complete_MissingAppointment_Returns404()
    {
        var svc = new FakeAppointmentService { CompleteThrows = new KeyNotFoundException() };
        var controller = BuildController(svc, Guid.NewGuid(), "staff");

        var result = await controller.Complete(ApptId, new CompleteAppointmentDto { UnitsDonated = 1 });

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Complete_ServiceRejectsOrg_Returns403()
    {
        var svc = new FakeAppointmentService { CompleteThrows = new UnauthorizedAccessException() };
        var controller = BuildController(svc, Guid.NewGuid(), "staff");

        var result = await controller.Complete(ApptId, new CompleteAppointmentDto { UnitsDonated = 1 });

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("staff", false)]
    public async Task Complete_PassesIsAdminFlagFromRoleClaim(string role, bool expectedIsAdmin)
    {
        var svc = new FakeAppointmentService();
        var controller = BuildController(svc, Guid.NewGuid(), role);

        await controller.Complete(ApptId, new CompleteAppointmentDto { UnitsDonated = 2 });

        Assert.Equal(expectedIsAdmin, svc.LastCompleteIsAdmin);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static AppointmentResponseDto AppointmentOwnedBy(Guid donorId) => new()
    {
        Id = ApptId,
        DonorId = donorId,
        OrganizationId = Guid.NewGuid(),
        ScheduledTime = DateTime.UtcNow.AddHours(3),
        Status = "scheduled",
    };

    private static AppointmentsController BuildController(
        IAppointmentService service, Guid userId, string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            ],
            authenticationType: "Test");

        return new AppointmentsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                },
            },
        };
    }

    private sealed class FakeAppointmentService : IAppointmentService
    {
        public AppointmentResponseDto? GetByIdResult { get; init; }
        public Exception? GetUpcomingThrows { get; init; }
        public Exception? CompleteThrows { get; init; }

        public bool? LastGetUpcomingIsAdmin { get; private set; }
        public bool? LastCompleteIsAdmin { get; private set; }

        public Task<AppointmentResponseDto?> GetByIdAsync(Guid id)
            => Task.FromResult(GetByIdResult);

        public Task<AppointmentResponseDto> UpdateStatusAsync(Guid id, UpdateAppointmentStatusDto dto)
            => Task.FromResult(new AppointmentResponseDto { Id = id, Status = dto.NewStatus });

        public Task<List<AppointmentResponseDto>> GetByDonorAsync(Guid donorId)
            => Task.FromResult(new List<AppointmentResponseDto>());

        public Task<PagedResultDto<AppointmentResponseDto>> GetUpcomingByOrganizationAsync(
            Guid organizationId, int page, int pageSize, Guid requestingUserId, bool isAdmin)
        {
            LastGetUpcomingIsAdmin = isAdmin;
            if (GetUpcomingThrows is not null) throw GetUpcomingThrows;
            return Task.FromResult(new PagedResultDto<AppointmentResponseDto>());
        }

        public Task<AppointmentResponseDto> CompleteAsync(
            Guid id, CompleteAppointmentDto dto, Guid requestingUserId, bool isAdmin)
        {
            LastCompleteIsAdmin = isAdmin;
            if (CompleteThrows is not null) throw CompleteThrows;
            return Task.FromResult(new AppointmentResponseDto { Id = id });
        }

        public Task<AppointmentResponseDto> CreateAsync(Guid donorId, CreateAppointmentDto dto)
            => Task.FromResult(new AppointmentResponseDto { Id = Guid.NewGuid(), DonorId = donorId });
    }
}
