using System.Security.Claims;
using BloodDonationNetwork.Api.Controllers;
using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodDonationNetwork.UnitTests.Requests;

public sealed class RequestsControllerDonorFilteringTests
{
    private static readonly Guid CallerId =
        Guid.Parse("ac0a01e6-6bfd-4871-99e4-bccd8c5d8123");

    [Fact]
    public async Task GetAll_DonorPassesTheirOwnIdAsDonorUserId()
    {
        var requestService = new RecordingRequestService();
        var controller = CreateController(
            requestService,
            UserRole.Donor);

        var result = await controller.GetAll();

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(CallerId, requestService.DonorUserId);
    }

    [Theory]
    [InlineData(UserRole.Staff)]
    [InlineData(UserRole.Admin)]
    public async Task GetAll_StaffAndAdminPassNullDonorUserId(
        UserRole role)
    {
        var requestService = new RecordingRequestService();
        var controller = CreateController(requestService, role);

        var result = await controller.GetAll();

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(requestService.DonorUserId);
    }

    private static RequestsController CreateController(
        IRequestService requestService,
        UserRole role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(
                    ClaimTypes.NameIdentifier,
                    CallerId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            ],
            "Test");

        return new RequestsController(
            requestService,
            new NoOpWorkflowService(),
            new NoOpHttpClientFactory(),
            NullLogger<RequestsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private sealed class RecordingRequestService : IRequestService
    {
        public Guid? DonorUserId { get; private set; }

        public Task<IEnumerable<RequestResponseDto>> GetAllAsync(
            Guid requestingUserId,
            UserRole requestingUserRole,
            BloodType? bloodType = null,
            RequestUrgency? urgency = null,
            string? status = null,
            Guid? organizationId = null,
            int page = 1,
            int pageSize = 10,
            string sortBy = "createdAt",
            bool descending = true,
            double? nearLat = null,
            double? nearLng = null,
            double? radiusKm = null,
            Guid? donorUserId = null)
        {
            DonorUserId = donorUserId;
            return Task.FromResult<IEnumerable<RequestResponseDto>>(
                Array.Empty<RequestResponseDto>());
        }

        public Task<RequestResponseDto> CreateAsync(
            Guid requesterId,
            Guid requestingUserId,
            bool isAdmin,
            CreateRequestDto dto) =>
            throw new NotSupportedException();

        public Task<RequestResponseDto?> GetByIdAsync(Guid id) =>
            throw new NotSupportedException();

        public Task<RequestResponseDto?> UpdateStatusAsync(
            Guid id,
            Guid requestingUserId,
            bool isAdmin,
            RequestStatusUpdateDto dto) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            Guid id,
            Guid requestingUserId,
            bool isAdmin) =>
            throw new NotSupportedException();

        public Task<RequestResponseDto?> CloseAsync(
            Guid id,
            Guid requestingUserId,
            bool isAdmin) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpWorkflowService : IAgentWorkflowService
    {
        public Task<AgentWorkflow?> GetByIdAsync(Guid workflowId) =>
            throw new NotSupportedException();

        public Task<AgentWorkflow?> GetLatestByBloodRequestIdAsync(
            Guid bloodRequestId) =>
            throw new NotSupportedException();

        public Task<List<AgentStep>> GetStepsAsync(Guid workflowId) =>
            throw new NotSupportedException();

        public Task<object?> GetSummaryAsync(Guid workflowId) =>
            throw new NotSupportedException();

        public Task<AgentWorkflow?> ApproveAsync(
            Guid workflowId,
            Guid decidedByUserId,
            string? comments) =>
            throw new NotSupportedException();

        public Task<AgentWorkflow?> RejectAsync(
            Guid workflowId,
            Guid decidedByUserId,
            string? comments) =>
            throw new NotSupportedException();

        public Task<AgentWorkflow?> ReviseAsync(
            Guid workflowId,
            Guid decidedByUserId,
            string? comments) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new NotSupportedException();
    }
}
