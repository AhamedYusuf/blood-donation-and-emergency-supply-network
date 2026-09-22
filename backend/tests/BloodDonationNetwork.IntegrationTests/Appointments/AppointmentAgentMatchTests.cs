using System.Text.Json;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.Infrastructure.Services;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Appointments;

// AppointmentResponseDto.AgentMatch is read back from the Matching &
// Dispatch Agent's own "search_donors" agent_steps row for an
// appointment's related workflow, so the console's "Agent reasoning"
// panel can show the actual rank/distance/reliability the agent used —
// not recomputed, and never fabricated when the data isn't there.
public sealed class AppointmentAgentMatchTests : IDisposable
{
    private static readonly Guid OrgId = Guid.Parse("0c000000-0000-0000-0000-000000000001");
    private static readonly Guid RequesterId = Guid.Parse("0c000000-0000-0000-0000-000000000002");
    private static readonly Guid BloodRequestId = Guid.Parse("0c000000-0000-0000-0000-000000000003");

    private static readonly Guid MatchedDonorUser = Guid.Parse("0c000000-0000-0000-0000-000000000010");
    private static readonly Guid MatchedDonorProfile = Guid.Parse("0c000000-0000-0000-0000-000000000011");
    private static readonly Guid UnmatchedDonorUser = Guid.Parse("0c000000-0000-0000-0000-000000000012");
    private static readonly Guid UnmatchedDonorProfile = Guid.Parse("0c000000-0000-0000-0000-000000000013");
    private static readonly Guid NoWorkflowDonorUser = Guid.Parse("0c000000-0000-0000-0000-000000000014");

    private static readonly Guid WorkflowWithStep = Guid.Parse("0c000000-0000-0000-0000-000000000020");
    private static readonly Guid WorkflowWithoutStep = Guid.Parse("0c000000-0000-0000-0000-000000000021");

    private static readonly Guid ApptMatched = Guid.Parse("0c000000-0000-0000-0000-000000000030");
    private static readonly Guid ApptDonorNotInCandidates = Guid.Parse("0c000000-0000-0000-0000-000000000031");
    private static readonly Guid ApptNoWorkflow = Guid.Parse("0c000000-0000-0000-0000-000000000032");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AppointmentAgentMatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var ctx = new TestAppDbContext(_options);
        ctx.Database.EnsureCreated();
        Seed(ctx);
    }

    public void Dispose() => _connection.Dispose();

    private AppointmentService NewService(TestAppDbContext ctx) =>
        new(ctx, new InventoryService(ctx));

    [Fact]
    public async Task AgentMatch_IsPopulated_FromTheAgentsOwnSearchStepOutput()
    {
        await using var ctx = new TestAppDbContext(_options);

        var page = await NewService(ctx).GetUpcomingByOrganizationAsync(
            OrgId, page: 1, pageSize: 20, requestingUserId: Guid.NewGuid(), isAdmin: true);

        var result = page.Items.Single(i => i.Id == ApptMatched);

        Assert.NotNull(result.AgentMatch);
        Assert.Equal(1, result.AgentMatch!.Rank);
        Assert.Equal(0.35, result.AgentMatch.DistanceKm);
        Assert.Equal(0.90m, result.AgentMatch.ReliabilityScore);
    }

    [Fact]
    public async Task AgentMatch_IsNull_WhenThisAppointmentsDonorIsntInTheStepsCandidateList()
    {
        await using var ctx = new TestAppDbContext(_options);

        var page = await NewService(ctx).GetUpcomingByOrganizationAsync(
            OrgId, page: 1, pageSize: 20, requestingUserId: Guid.NewGuid(), isAdmin: true);

        var result = page.Items.Single(i => i.Id == ApptDonorNotInCandidates);

        Assert.Null(result.AgentMatch);
    }

    [Fact]
    public async Task AgentMatch_IsNull_WhenTheAppointmentHasNoRelatedWorkflow()
    {
        await using var ctx = new TestAppDbContext(_options);

        var page = await NewService(ctx).GetUpcomingByOrganizationAsync(
            OrgId, page: 1, pageSize: 20, requestingUserId: Guid.NewGuid(), isAdmin: true);

        var result = page.Items.Single(i => i.Id == ApptNoWorkflow);

        Assert.Null(result.AgentMatch);
    }

    private static void Seed(TestAppDbContext ctx)
    {
        ctx.Organizations.Add(new Organization
        {
            Id = OrgId,
            Name = "Test Blood Bank",
            Type = OrganizationType.BloodBank,
            Address = "1 Test Rd",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        ctx.Users.AddRange(
            NewUser(RequesterId),
            NewUser(MatchedDonorUser),
            NewUser(UnmatchedDonorUser),
            NewUser(NoWorkflowDonorUser));

        ctx.DonorProfiles.AddRange(
            NewDonorProfile(MatchedDonorUser, MatchedDonorProfile),
            NewDonorProfile(UnmatchedDonorUser, UnmatchedDonorProfile));

        ctx.BloodRequests.Add(new BloodRequest
        {
            Id = BloodRequestId,
            RequesterId = RequesterId,
            OrganizationId = OrgId,
            BloodType = BloodType.OPositive,
            UnitsRequested = 2,
            Urgency = RequestUrgency.Critical,
            Status = RequestStatuses.Open,
            HospitalName = "Test Hospital",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CreatedAt = DateTime.UtcNow,
        });

        ctx.AgentWorkflows.AddRange(
            NewWorkflow(WorkflowWithStep),
            NewWorkflow(WorkflowWithoutStep));

        // Only MatchedDonorProfile appears in this step's candidate list —
        // UnmatchedDonorProfile deliberately does not, to cover the "agent
        // ran a search but this particular donor wasn't a candidate in it"
        // case (e.g. a manually-booked appointment later linked to an
        // unrelated workflow id).
        var outputJson = JsonSerializer.Serialize(new
        {
            Candidates = new[]
            {
                new
                {
                    DonorId = MatchedDonorProfile,
                    DistanceKm = 0.35,
                    ReliabilityScore = 0.90m,
                    Rank = 1,
                },
            },
        });

        ctx.AgentSteps.Add(new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = WorkflowWithStep,
            AgentName = AgentNames.MatchingDispatch,
            StepName = "search_donors",
            Status = "completed",
            OutputJson = outputJson,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        });

        var soon = DateTime.UtcNow.AddHours(6);
        ctx.DonationAppointments.AddRange(
            NewAppointment(ApptMatched, MatchedDonorUser, WorkflowWithStep, soon),
            NewAppointment(ApptDonorNotInCandidates, UnmatchedDonorUser, WorkflowWithStep, soon.AddHours(1)),
            NewAppointment(ApptNoWorkflow, NoWorkflowDonorUser, relatedWorkflowId: null, soon.AddHours(2)));

        ctx.SaveChanges();
    }

    private static User NewUser(Guid id) => new()
    {
        Id = id,
        Email = $"{id}@example.com",
        PasswordHash = "not-a-real-hash",
        Role = UserRole.Donor,
        FullName = "Test Donor",
        PhoneNumber = "0000000000",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static DonorProfile NewDonorProfile(Guid userId, Guid profileId) => new()
    {
        Id = profileId,
        UserId = userId,
        BloodType = "O+",
        DateOfBirth = new DateOnly(1990, 1, 1),
        VerifiedByAdmin = true,
        ReliabilityScore = 1.0m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static AgentWorkflow NewWorkflow(Guid id) => new()
    {
        Id = id,
        BloodRequestId = BloodRequestId,
        Status = WorkflowStatuses.Approved,
        Objective = "test workflow",
        StartedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static DonationAppointment NewAppointment(
        Guid id, Guid donorId, Guid? relatedWorkflowId, DateTime when) => new()
    {
        Id = id,
        DonorId = donorId,
        OrganizationId = OrgId,
        RelatedWorkflowId = relatedWorkflowId,
        ScheduledTime = when,
        Status = AppointmentStatus.Scheduled,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
