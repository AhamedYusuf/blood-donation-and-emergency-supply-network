using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Api.Controllers.Internal;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Workflows;

public class AgentWorkflowServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestAppDbContext _context;
    private readonly AgentWorkflowService _service;

    public AgentWorkflowServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

        _context = new TestAppDbContext(options);

        _context.Database.EnsureCreated();

        // These tests focus only on workflow logic.
        // Avoid unrelated FK setup for BloodRequest/User/Organization.
        _context.Database.ExecuteSqlRaw(
            "PRAGMA foreign_keys = OFF;");

        _service = new AgentWorkflowService(_context);
    }

    private async Task<AgentWorkflow> CreateWorkflowAsync(
        string status = WorkflowStatuses.AwaitingApproval,
        int revisionCount = 0,
        Guid? bloodRequestId = null)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            BloodRequestId = bloodRequestId ?? Guid.NewGuid(),
            Status = status,
            RevisionCount = revisionCount,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AgentWorkflows.Add(workflow);
        await _context.SaveChangesAsync();

        return workflow;
    }

    private async Task<(BloodRequest Request, AgentWorkflow Workflow)>
        CreateWorkflowWithRequestAsync(
            string requestStatus = RequestStatuses.AwaitingApproval,
            string workflowStatus = WorkflowStatuses.AwaitingApproval,
            int revisionCount = 0)
    {
        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Status = requestStatus,
            Notes = string.Empty,
        };
        _context.BloodRequests.Add(request);
        await _context.SaveChangesAsync();

        var workflow = await CreateWorkflowAsync(
            workflowStatus,
            revisionCount,
            request.Id);

        return (request, workflow);
    }

    [Fact]
    public async Task ApproveAsync_SetsWorkflowToApproved()
    {
        var workflow = await CreateWorkflowAsync();
        var userId = Guid.NewGuid();

        var result = await _service.ApproveAsync(
            workflow.Id,
            userId,
            "Approved by staff");

        Assert.NotNull(result);
        Assert.Equal(
            WorkflowStatuses.Approved,
            result.Status);

        var decision =
            await _context.ApprovalDecisions
                .FirstOrDefaultAsync(
                    d => d.WorkflowId == workflow.Id);

        Assert.NotNull(decision);
        Assert.Equal(
            ApprovalDecisions.Approved,
            decision.Decision);
        Assert.Equal(userId, decision.DecidedByUserId);
    }

    [Fact]
    public async Task ApproveAsync_MovesAwaitingRequestToMatching()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync();

        await _service.ApproveAsync(
            workflow.Id,
            Guid.NewGuid(),
            null);

        Assert.Equal(
            RequestStatuses.Matching,
            request.Status);
    }

    [Fact]
    public async Task RejectAsync_SetsWorkflowToRejected()
    {
        var workflow = await CreateWorkflowAsync();
        var userId = Guid.NewGuid();

        var result = await _service.RejectAsync(
            workflow.Id,
            userId,
            "Request rejected");

        Assert.NotNull(result);
        Assert.Equal(
            WorkflowStatuses.Rejected,
            result.Status);

        Assert.NotNull(result.CompletedAt);

        var decision =
            await _context.ApprovalDecisions
                .FirstOrDefaultAsync(
                    d => d.WorkflowId == workflow.Id);

        Assert.NotNull(decision);
        Assert.Equal(
            ApprovalDecisions.Rejected,
            decision.Decision);
    }

    [Fact]
    public async Task RejectAsync_CancelsAwaitingRequest()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync();

        await _service.RejectAsync(
            workflow.Id,
            Guid.NewGuid(),
            null);

        Assert.Equal(
            RequestStatuses.Cancelled,
            request.Status);
        Assert.NotNull(request.ClosedAt);
    }

    [Fact]
    public async Task RejectAsync_DoesNotOverwriteManuallyChangedRequestStatus()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync(
                requestStatus: RequestStatuses.Fulfilled);

        await _service.RejectAsync(
            workflow.Id,
            Guid.NewGuid(),
            null);

        Assert.Equal(
            RequestStatuses.Fulfilled,
            request.Status);
    }

    [Fact]
    public async Task ReviseAsync_IncreasesRevisionCount()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync();
        var userId = Guid.NewGuid();

        var result = await _service.ReviseAsync(
            workflow.Id,
            userId,
            "Please revise");

        Assert.NotNull(result);

        Assert.Equal(
            WorkflowStatuses.RevisionRequested,
            result.Status);

        Assert.Equal(1, result.RevisionCount);

        var decision =
            await _context.ApprovalDecisions
                .FirstOrDefaultAsync(
                    d => d.WorkflowId == workflow.Id);

        Assert.NotNull(decision);

        Assert.Equal(
            ApprovalDecisions.RevisionRequested,
            decision.Decision);
        Assert.Equal(
            RequestStatuses.Matching,
            request.Status);
    }

    [Fact]
    public async Task ReviseAsync_ThirdRevisionIsAllowed()
    {
        var workflow = await CreateWorkflowAsync(
            WorkflowStatuses.AwaitingApproval,
            revisionCount: 2);

        var result = await _service.ReviseAsync(
            workflow.Id,
            Guid.NewGuid(),
            "Third revision");

        Assert.NotNull(result);

        Assert.Equal(3, result.RevisionCount);

        Assert.Equal(
            WorkflowStatuses.RevisionRequested,
            result.Status);

        Assert.Null(result.CompletedAt);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task ReviseAsync_FourthRevisionFailsWorkflow()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync(
                revisionCount: 3);

        var result = await _service.ReviseAsync(
            workflow.Id,
            Guid.NewGuid(),
            "Fourth revision");

        Assert.NotNull(result);

        Assert.Equal(3, result.RevisionCount);

        Assert.Equal(
            WorkflowStatuses.Failed,
            result.Status);

        Assert.Equal(
            "Maximum workflow revision limit exceeded.",
            result.FailureReason);

        Assert.NotNull(result.CompletedAt);
        Assert.Equal(
            RequestStatuses.Open,
            request.Status);
    }

    [Fact]
    public async Task UpdateStatus_WhenWorkflowCompletes_MarksOpenRequestAsDonorsNotified()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync(
                requestStatus: RequestStatuses.Open,
                workflowStatus: WorkflowStatuses.Approved);
        var controller =
            new WorkflowInternalController(
                _context,
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<WorkflowInternalController>.Instance);

        var result = await controller.UpdateStatus(
            workflow.Id,
            new UpdateWorkflowStatusInternalRequest
            {
                Status = WorkflowStatuses.Completed,
            });

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        Assert.Equal(
            RequestStatuses.DonorsNotified,
            request.Status);

        _context.Entry(request).State =
            EntityState.Detached;

        var persistedRequest =
            await _context.BloodRequests
                .AsNoTracking()
                .FirstAsync(item => item.Id == request.Id);

        Assert.Equal(
            RequestStatuses.DonorsNotified,
            persistedRequest.Status);

        var requestService =
            new RequestService(_context);

        var requestResponse =
            await requestService.GetByIdAsync(
                request.Id);
        var dashboardRequests =
            await requestService.GetAllAsync(
                pageSize: 100);

        Assert.NotNull(requestResponse);
        Assert.Equal(
            RequestStatuses.DonorsNotified,
            requestResponse.Status);
        Assert.Contains(
            dashboardRequests,
            item => item.Id == request.Id &&
                    item.Status ==
                        RequestStatuses.DonorsNotified);
    }

    [Fact]
    public async Task ApproveAndCompleteWorkflow_PersistsBloodRequestStatusReturnedByRequestApi()
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync(
                requestStatus: RequestStatuses.Open);

        var approval = await _service.ApproveAsync(
            workflow.Id,
            Guid.NewGuid(),
            "Approved for dispatch");

        Assert.NotNull(approval);
        Assert.Equal(
            RequestStatuses.Matching,
            request.Status);

        var internalController =
            new WorkflowInternalController(
                _context,
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<WorkflowInternalController>.Instance);
        var completionResult =
            await internalController.UpdateStatus(
                workflow.Id,
                new UpdateWorkflowStatusInternalRequest
                {
                    Status = WorkflowStatuses.Completed,
                });

        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(
            completionResult);

        _context.Entry(request).State =
            EntityState.Detached;

        var persistedRequest =
            await _context.BloodRequests
                .AsNoTracking()
                .FirstAsync(item => item.Id == request.Id);
        var requestResponse =
            await new RequestService(_context)
                .GetByIdAsync(request.Id);

        Assert.Equal(
            RequestStatuses.DonorsNotified,
            persistedRequest.Status);
        Assert.NotNull(requestResponse);
        Assert.Equal(
            RequestStatuses.DonorsNotified,
            requestResponse.Status);
    }

    [Theory]
    [InlineData(RequestStatuses.Matching)]
    [InlineData(RequestStatuses.AwaitingApproval)]
    public async Task UpdateStatus_WhenWorkflowCompletes_AdvancesActiveRequestStatus(
        string requestStatus)
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync(
                requestStatus: requestStatus,
                workflowStatus: WorkflowStatuses.Approved);
        var controller =
            new WorkflowInternalController(
                _context,
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<WorkflowInternalController>.Instance);

        await controller.UpdateStatus(
            workflow.Id,
            new UpdateWorkflowStatusInternalRequest
            {
                Status = WorkflowStatuses.Completed,
            });

        Assert.Equal(
            RequestStatuses.DonorsNotified,
            request.Status);
    }

    [Theory]
    [InlineData(RequestStatuses.Fulfilled)]
    [InlineData(RequestStatuses.Cancelled)]
    public async Task UpdateStatus_WhenWorkflowCompletes_DoesNotOverwriteManualTerminalStatus(
        string requestStatus)
    {
        var (request, workflow) =
            await CreateWorkflowWithRequestAsync(
                requestStatus: requestStatus,
                workflowStatus: WorkflowStatuses.Approved);
        var controller =
            new WorkflowInternalController(
                _context,
                Microsoft.Extensions.Logging.Abstractions
                    .NullLogger<WorkflowInternalController>.Instance);

        await controller.UpdateStatus(
            workflow.Id,
            new UpdateWorkflowStatusInternalRequest
            {
                Status = WorkflowStatuses.Completed,
            });

        Assert.Equal(
            requestStatus,
            request.Status);
    }

    [Fact]
    public async Task SecondDecision_WhenWorkflowAlreadyApproved_ThrowsException()
    {
        var workflow = await CreateWorkflowAsync(
            WorkflowStatuses.Approved);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.RejectAsync(
                    workflow.Id,
                    Guid.NewGuid(),
                    "Trying another decision"));

        Assert.Contains(
            "cannot receive another approval decision",
            exception.Message);
    }

    [Fact]
    public async Task ApproveAsync_ReturnsNull_WhenWorkflowDoesNotExist()
    {
        var result = await _service.ApproveAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsWorkflow()
    {
        var workflow = await CreateWorkflowAsync();

        var result = await _service.GetByIdAsync(
            workflow.Id);

        Assert.NotNull(result);
        Assert.Equal(
            workflow.Id,
            result.Id);

        Assert.Equal(
            workflow.BloodRequestId,
            result.BloodRequestId);

        Assert.Equal(
            WorkflowStatuses.AwaitingApproval,
            result.Status);
    }

    [Fact]
    public async Task GetStepsAsync_ReturnsStepsInStartedAtOrder()
    {
        var workflow = await CreateWorkflowAsync();

        var baseTime = DateTime.UtcNow;

        var laterStep = new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            AgentName = "Agent B",
            StepName = "second_step",
            Status = "completed",
            StartedAt = baseTime.AddMinutes(2),
            CompletedAt = baseTime.AddMinutes(3)
        };

        var earlierStep = new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            AgentName = "Agent A",
            StepName = "first_step",
            Status = "completed",
            StartedAt = baseTime,
            CompletedAt = baseTime.AddMinutes(1)
        };

        _context.AgentSteps.AddRange(
            laterStep,
            earlierStep);

        await _context.SaveChangesAsync();

        var result =
            await _service.GetStepsAsync(
                workflow.Id);

        Assert.Equal(2, result.Count);

        Assert.Equal(
            "first_step",
            result[0].StepName);

        Assert.Equal(
            "second_step",
            result[1].StepName);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsWorkflowSummary()
    {
        var workflow = await CreateWorkflowAsync();

        var baseTime = DateTime.UtcNow;

        var completedStep = new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            AgentName = "Agent A",
            StepName = "completed_step",
            Status = "completed",
            StartedAt = baseTime,
            CompletedAt = baseTime.AddSeconds(1),
            ErrorMessage = null
        };

        var failedStep = new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            AgentName = "Agent B",
            StepName = "failed_step",
            Status = "failed",
            StartedAt = baseTime.AddSeconds(2),
            CompletedAt = baseTime.AddSeconds(3),
            ErrorMessage = "Test failure"
        };

        _context.AgentSteps.AddRange(
            completedStep,
            failedStep);

        await _context.SaveChangesAsync();

        var result =
            await _service.GetSummaryAsync(
                workflow.Id);

        Assert.NotNull(result);

        var json =
            System.Text.Json.JsonSerializer.Serialize(
                result);

        using var document =
            System.Text.Json.JsonDocument.Parse(
                json);

        var root =
            document.RootElement;

        Assert.Equal(
            workflow.Id,
            root.GetProperty("Id")
                .GetGuid());

        Assert.Equal(
            workflow.BloodRequestId,
            root.GetProperty("BloodRequestId")
                .GetGuid());

        Assert.Equal(
            2,
            root.GetProperty("TotalSteps")
                .GetInt32());

        Assert.Equal(
            1,
            root.GetProperty("CompletedSteps")
                .GetInt32());

        Assert.Equal(
            1,
            root.GetProperty("FailedSteps")
                .GetInt32());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}