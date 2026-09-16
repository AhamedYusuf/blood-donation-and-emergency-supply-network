using BloodDonationNetwork.Application.DTOs.Agents;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainBloodType = BloodDonationNetwork.Domain.Enums.BloodType;

namespace BloodDonationNetwork.IntegrationTests.Agents;

// Tech Doc §4.4 Mode 2 + §4.7's explicitly-named marking-scheme item:
// "reject dispatch calls on non-approved workflows". Runs against SQLite
// in-memory (real FK enforcement), not the EF in-memory provider.
public sealed class MatchingDispatchAgentServiceTests : IDisposable
{
    private static readonly Guid OrgId = Guid.Parse("0d000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffUserId = Guid.Parse("0d000000-0000-0000-0000-000000000002");
    private static readonly Guid DonorUserId = Guid.Parse("0d000000-0000-0000-0000-000000000003");
    private static readonly Guid DonorProfileId = Guid.Parse("0d000000-0000-0000-0000-000000000004");
    private static readonly Guid BloodRequestId = Guid.Parse("0d000000-0000-0000-0000-000000000005");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MatchingDispatchAgentServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var ctx = new TestAppDbContext(_options);
        ctx.Database.EnsureCreated();
        Seed(ctx);
    }

    public void Dispose() => _connection.Dispose();

    private static MatchingDispatchAgentService NewService(TestAppDbContext ctx) => new(
        ctx,
        new DonorRankingCalculator(),
        new AppointmentService(ctx, new InventoryService(ctx)),
        new NotificationService(ctx, new FakeFcm()));

    private sealed class FakeFcm : IFcmSender
    {
        public Task<FcmSendOutcome> SendAsync(
            string deviceToken, string title, string body,
            IReadOnlyDictionary<string, string>? data = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(FcmSendOutcome.Delivered);
    }

    [Fact]
    public async Task DispatchAsync_RejectsCall_WhenWorkflowIsNotApproved()
    {
        var workflowId = await SeedWorkflowAsync(WorkflowStatuses.AwaitingApproval);

        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        var request = new DispatchRequestDto
        {
            WorkflowId = workflowId,
            Eligible = new List<DispatchCandidateDto> { new() { DonorId = DonorProfileId } },
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DispatchAsync(request));
        Assert.Contains("not approved", ex.Message);

        // No side effects: no appointment created, no workflow mutated.
        await using var verify = new TestAppDbContext(_options);
        Assert.False(await verify.DonationAppointments.AnyAsync(a => a.RelatedWorkflowId == workflowId));
    }

    [Theory]
    [InlineData(WorkflowStatuses.Planning)]
    [InlineData(WorkflowStatuses.Rejected)]
    [InlineData(WorkflowStatuses.Completed)]
    public async Task DispatchAsync_RejectsCall_ForEveryNonApprovedStatus(string status)
    {
        var workflowId = await SeedWorkflowAsync(status);

        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DispatchAsync(new DispatchRequestDto
        {
            WorkflowId = workflowId,
            Eligible = new List<DispatchCandidateDto> { new() { DonorId = DonorProfileId } },
        }));
    }

    [Fact]
    public async Task DispatchAsync_UnknownWorkflow_ThrowsKeyNotFound()
    {
        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.DispatchAsync(new DispatchRequestDto
        {
            WorkflowId = Guid.NewGuid(),
            Eligible = new List<DispatchCandidateDto>(),
        }));
    }

    [Fact]
    public async Task DispatchAsync_ApprovedWorkflow_CreatesAppointmentAndNotifies()
    {
        var workflowId = await SeedWorkflowAsync(WorkflowStatuses.Approved);

        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        var result = await sut.DispatchAsync(new DispatchRequestDto
        {
            WorkflowId = workflowId,
            Eligible = new List<DispatchCandidateDto> { new() { DonorId = DonorProfileId } },
        });

        Assert.Equal(1, result.AppointmentsCreated);
        Assert.Equal(1, result.NotificationsDelivered);
        Assert.Single(result.Results);
        Assert.Null(result.Results[0].FailureReason);
        Assert.True(result.Results[0].AppointmentId.HasValue);

        await using var verify = new TestAppDbContext(_options);
        var appointment = await verify.DonationAppointments.SingleAsync(a => a.RelatedWorkflowId == workflowId);
        // DonorId on the appointment is the donor's User.Id, not the
        // DonorProfile.Id the request came in with — the service must
        // resolve that itself.
        Assert.Equal(DonorUserId, appointment.DonorId);
        Assert.Equal(OrgId, appointment.OrganizationId);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    // Tech Doc §0.7 — every agent action writes a row to the shared
    // agent_steps table.
    [Fact]
    public async Task DispatchAsync_ApprovedWorkflow_LogsCompletedAgentStep()
    {
        var workflowId = await SeedWorkflowAsync(WorkflowStatuses.Approved);

        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        await sut.DispatchAsync(new DispatchRequestDto
        {
            WorkflowId = workflowId,
            Eligible = new List<DispatchCandidateDto> { new() { DonorId = DonorProfileId } },
        });

        await using var verify = new TestAppDbContext(_options);
        var step = await verify.AgentSteps.SingleAsync(s => s.WorkflowId == workflowId);
        Assert.Equal(AgentNames.MatchingDispatch, step.AgentName);
        Assert.Equal("dispatch", step.StepName);
        Assert.Equal("completed", step.Status);
        Assert.Null(step.ErrorMessage);
        Assert.NotNull(step.OutputJson);
        Assert.NotNull(step.CompletedAt);
    }

    [Fact]
    public async Task DispatchAsync_RejectedForNonApprovedWorkflow_LogsFailedAgentStep()
    {
        var workflowId = await SeedWorkflowAsync(WorkflowStatuses.AwaitingApproval);

        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DispatchAsync(new DispatchRequestDto
        {
            WorkflowId = workflowId,
            Eligible = new List<DispatchCandidateDto> { new() { DonorId = DonorProfileId } },
        }));

        await using var verify = new TestAppDbContext(_options);
        var step = await verify.AgentSteps.SingleAsync(s => s.WorkflowId == workflowId);
        Assert.Equal("failed", step.Status);
        Assert.Contains("not approved", step.ErrorMessage);
    }

    [Fact]
    public async Task SearchDonorsAsync_WithRealWorkflow_LogsCompletedAgentStep()
    {
        var workflowId = await SeedWorkflowAsync(WorkflowStatuses.Planning);

        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        await sut.SearchDonorsAsync(new SearchDonorsRequestDto
        {
            WorkflowId = workflowId,
            BloodType = "O+",
            Latitude = 6.93,
            Longitude = 79.86,
            RadiusKm = 50,
            UrgencyLevel = "critical",
        });

        await using var verify = new TestAppDbContext(_options);
        var step = await verify.AgentSteps.SingleAsync(s => s.WorkflowId == workflowId);
        Assert.Equal(AgentNames.MatchingDispatch, step.AgentName);
        Assert.Equal("search_donors", step.StepName);
        Assert.Equal("completed", step.Status);
    }

    [Fact]
    public async Task SearchDonorsAsync_WithoutAMatchingWorkflow_SkipsLoggingWithoutThrowing()
    {
        // Mode 1 predates AgentWorkflow and is still called ad hoc (manual
        // curl tests, callers with no tracked workflow) — an unrecognized
        // WorkflowId must not break the search itself.
        await using var ctx = new TestAppDbContext(_options);
        var sut = NewService(ctx);

        var result = await sut.SearchDonorsAsync(new SearchDonorsRequestDto
        {
            WorkflowId = Guid.NewGuid(),
            BloodType = "O+",
            Latitude = 6.93,
            Longitude = 79.86,
            RadiusKm = 50,
            UrgencyLevel = "critical",
        });

        Assert.Single(result.Candidates);

        await using var verify = new TestAppDbContext(_options);
        Assert.Equal(0, await verify.AgentSteps.CountAsync());
    }

    private async Task<Guid> SeedWorkflowAsync(string status)
    {
        var workflowId = Guid.NewGuid();
        await using var ctx = new TestAppDbContext(_options);
        ctx.AgentWorkflows.Add(new AgentWorkflow
        {
            Id = workflowId,
            BloodRequestId = BloodRequestId,
            Status = status,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return workflowId;
    }

    private static void Seed(TestAppDbContext ctx)
    {
        ctx.Organizations.Add(new Organization
        {
            Id = OrgId,
            Name = "Test Hospital",
            Type = OrganizationType.Hospital,
            Address = "1 Test Rd",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.Users.Add(new User
        {
            Id = StaffUserId,
            Email = "staff@test.local",
            PasswordHash = "x",
            Role = UserRole.Staff,
            FullName = "Test Staff",
            PhoneNumber = "000",
            OrganizationId = OrgId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.Users.Add(new User
        {
            Id = DonorUserId,
            Email = "donor@test.local",
            PasswordHash = "x",
            Role = UserRole.Donor,
            FullName = "Test Donor",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.DonorProfiles.Add(new DonorProfile
        {
            Id = DonorProfileId,
            UserId = DonorUserId,
            BloodType = "O+",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Latitude = 6.93,
            Longitude = 79.86,
            VerifiedByAdmin = true,
            ReliabilityScore = 1.0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.DonorDevices.Add(new DonorDevice
        {
            Id = Guid.NewGuid(),
            DonorUserId = DonorUserId,
            FcmToken = "test-fcm-token",
            Platform = "android",
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        });

        ctx.BloodRequests.Add(new BloodRequest
        {
            Id = BloodRequestId,
            RequesterId = StaffUserId,
            OrganizationId = OrgId,
            BloodType = DomainBloodType.OPositive,
            UnitsRequested = 2,
            Urgency = RequestUrgency.Critical,
            HospitalName = "Test Hospital",
            Latitude = 6.93,
            Longitude = 79.86,
            CreatedAt = DateTime.UtcNow,
        });

        ctx.SaveChanges();
    }
}
