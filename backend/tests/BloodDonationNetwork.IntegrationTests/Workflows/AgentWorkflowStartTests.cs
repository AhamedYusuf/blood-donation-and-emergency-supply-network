using System.Net;
using System.Security.Claims;
using System.Text.Json;
using BloodDonationNetwork.Api.Controllers;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Api.Controllers.Internal;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloodDonationNetwork.IntegrationTests.Workflows;

public sealed class AgentWorkflowStartTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestAppDbContext _context;
    private readonly AgentWorkflowService _workflowService;

    public AgentWorkflowStartTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

        _context = new TestAppDbContext(options);
        _context.Database.EnsureCreated();
        _context.Database.ExecuteSqlRaw(
            "PRAGMA foreign_keys = OFF;");

        _workflowService =
            new AgentWorkflowService(_context);
    }

    [Fact]
    public async Task StartWorkflowForBloodRequest_CreatesAndReturnsPersistedWorkflow()
    {
        var adminId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var request = CreateRequest(
            adminId,
            organizationId);
        _context.BloodRequests.Add(request);
        await _context.SaveChangesAsync();

        var agentHandler =
            new CallbackHttpMessageHandler(
                async (_, cancellationToken) =>
                {
                    _context.AgentWorkflows.Add(
                        new AgentWorkflow
                        {
                            Id = Guid.NewGuid(),
                            BloodRequestId = request.Id,
                            Status = WorkflowStatuses.Planning,
                            Objective = "Test workflow",
                            StartedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    await _context.SaveChangesAsync(
                        cancellationToken);

                    return new HttpResponseMessage(
                        HttpStatusCode.OK);
                });

        var controller = CreateController(
            agentHandler,
            adminId,
            UserRoles.Admin);

        var result =
            await controller.StartWorkflowForBloodRequest(
                request.Id);

        Assert.IsType<OkObjectResult>(result);
        var persistedWorkflow =
            await _workflowService
                .GetLatestByBloodRequestIdAsync(
                    request.Id);
        Assert.NotNull(persistedWorkflow);
        Assert.Equal(
            WorkflowStatuses.Planning,
            persistedWorkflow.Status);
    }

    [Fact]
    public async Task StartWorkflowForBloodRequest_ReturnsExistingWorkflowWithoutRestartingIt()
    {
        var adminId = Guid.NewGuid();
        var request = CreateRequest(
            adminId,
            Guid.NewGuid());
        var existingWorkflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            BloodRequestId = request.Id,
            Status = WorkflowStatuses.AwaitingApproval,
            Objective = "Existing workflow",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.BloodRequests.Add(request);
        _context.AgentWorkflows.Add(existingWorkflow);
        await _context.SaveChangesAsync();

        var agentHandler =
            new CallbackHttpMessageHandler(
                (_, _) => throw new InvalidOperationException(
                    "Agent service must not be called for an existing workflow."));
        var controller = CreateController(
            agentHandler,
            adminId,
            UserRoles.Admin);

        var result =
            await controller.StartWorkflowForBloodRequest(
                request.Id);

        var okResult =
            Assert.IsType<OkObjectResult>(result);
        using var responseJson =
            JsonDocument.Parse(
                JsonSerializer.Serialize(okResult.Value));
        Assert.Equal(
            existingWorkflow.Id,
            responseJson.RootElement
                    .GetProperty("Id")
                .GetGuid());
    }

    [Fact]
    public async Task StartWorkflowForBloodRequest_ForbidsStaffFromAnotherOrganization()
    {
        var staffId = Guid.NewGuid();
        var requestOrganizationId = Guid.NewGuid();
        var staff = new User
        {
            Id = staffId,
            Email = "staff@test.local",
            PasswordHash = "test",
            Role = UserRole.Staff,
            FullName = "Test Staff",
            PhoneNumber = "000",
            OrganizationId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var request = CreateRequest(
            staffId,
            requestOrganizationId);
        _context.Users.Add(staff);
        _context.BloodRequests.Add(request);
        await _context.SaveChangesAsync();

        var agentHandler =
            new CallbackHttpMessageHandler(
                (_, _) => throw new InvalidOperationException(
                    "Agent service must not be called for unauthorized staff."));
        var controller = CreateController(
            agentHandler,
            staffId,
            UserRoles.Staff);

        var result =
            await controller.StartWorkflowForBloodRequest(
                request.Id);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task InternalCreateWorkflow_IsIdempotentForExistingRequest()
    {
        var adminId = Guid.NewGuid();
        var request = CreateRequest(adminId, Guid.NewGuid());
        _context.BloodRequests.Add(request);
        await _context.SaveChangesAsync();

        var controller = new WorkflowInternalController(
            _context,
            NullLogger<WorkflowInternalController>.Instance);

        var firstResult = Assert.IsType<CreatedResult>(
            await controller.CreateWorkflow(
                new CreateWorkflowInternalRequest
                {
                    BloodRequestId = request.Id
                }));
        var firstId = GetWorkflowId(firstResult.Value);

        var secondResult = Assert.IsType<OkObjectResult>(
            await controller.CreateWorkflow(
                new CreateWorkflowInternalRequest
                {
                    BloodRequestId = request.Id
                }));
        var secondId = GetWorkflowId(secondResult.Value);

        Assert.Equal(firstId, secondId);
        Assert.Equal(
            1,
            await _context.AgentWorkflows.CountAsync(
                workflow =>
                    workflow.BloodRequestId == request.Id));
        Assert.True(
            JsonDocument.Parse(
                    JsonSerializer.Serialize(secondResult.Value))
                .RootElement
                .GetProperty("alreadyExists")
                .GetBoolean());
    }

    private AgentWorkflowsController CreateController(
        HttpMessageHandler handler,
        Guid userId,
        string role)
    {
        var controller =
            new AgentWorkflowsController(
                _workflowService,
                new TestHttpClientFactory(handler),
                _context,
                NullLogger<AgentWorkflowsController>.Instance);

        var identity =
            new ClaimsIdentity(
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId.ToString()),
                    new Claim(ClaimTypes.Role, role)
                },
                "integration-test");
        controller.ControllerContext =
            new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

        return controller;
    }

    private static BloodRequest CreateRequest(
        Guid requesterId,
        Guid organizationId)
    {
        return new BloodRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            OrganizationId = organizationId,
            BloodType = BloodType.APositive,
            UnitsRequested = 1,
            Urgency = RequestUrgency.Normal,
            Status = RequestStatuses.Open,
            Notes = string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Guid GetWorkflowId(object? response)
    {
        using var json =
            JsonDocument.Parse(JsonSerializer.Serialize(response));
        return json.RootElement
            .GetProperty("Id")
            .GetGuid();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class TestHttpClientFactory(
        HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(
                handler,
                disposeHandler: false)
            {
                BaseAddress =
                    new Uri("http://agent-service.test")
            };
        }
    }

    private sealed class CallbackHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return callback(
                request,
                cancellationToken);
        }
    }
}
