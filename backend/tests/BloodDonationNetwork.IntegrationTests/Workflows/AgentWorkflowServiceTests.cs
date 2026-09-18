using BloodDonationNetwork.Application.Services;
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
        int revisionCount = 0)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            BloodRequestId = Guid.NewGuid(),
            Status = status,
            RevisionCount = revisionCount,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AgentWorkflows.Add(workflow);
        await _context.SaveChangesAsync();

        return workflow;
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
    public async Task ReviseAsync_IncreasesRevisionCount()
    {
        var workflow = await CreateWorkflowAsync();
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
        var workflow = await CreateWorkflowAsync(
            WorkflowStatuses.AwaitingApproval,
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

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}