using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using BloodDonationNetwork.Infrastructure.Persistence;
using BloodDonationNetwork.IntegrationTests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.IntegrationTests.Requests;

public sealed class RequestVisibilityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestAppDbContext _context;
    private readonly RequestService _service;

    private readonly Guid _organizationOneId = Guid.NewGuid();
    private readonly Guid _organizationTwoId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _donorId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private readonly Guid _unassignedStaffId = Guid.NewGuid();

    public RequestVisibilityTests()
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

        _service = new RequestService(_context);
    }

    [Fact]
    public async Task Admin_CanSeeRequestsFromMultipleOrganizations()
    {
        await SeedAsync();

        var requests = (await _service.GetAllAsync(
            _adminId,
            UserRole.Admin,
            pageSize: 100)).ToList();

        Assert.Equal(3, requests.Count);
        Assert.Contains(
            requests,
            request =>
                request.OrganizationId == _organizationOneId);
        Assert.Contains(
            requests,
            request =>
                request.OrganizationId == _organizationTwoId);
    }

    [Fact]
    public async Task Staff_SeesOnlyRequestsFromAssignedOrganization()
    {
        await SeedAsync();

        var requests = (await _service.GetAllAsync(
            _staffId,
            UserRole.Staff,
            pageSize: 100)).ToList();

        Assert.Equal(2, requests.Count);
        Assert.All(
            requests,
            request => Assert.Equal(
                _organizationOneId,
                request.OrganizationId));
    }

    [Fact]
    public async Task Staff_CannotSeeAnotherOrganizationsRequest()
    {
        await SeedAsync();

        var requests = await _service.GetAllAsync(
            _staffId,
            UserRole.Staff,
            organizationId: _organizationTwoId,
            pageSize: 100);

        Assert.Empty(requests);
    }

    [Fact]
    public async Task StaffWithoutOrganization_ReceivesNoRequests()
    {
        await SeedAsync();

        var requests = await _service.GetAllAsync(
            _unassignedStaffId,
            UserRole.Staff,
            pageSize: 100);

        Assert.Empty(requests);
    }

    [Fact]
    public async Task Donor_CanSeeRequestsFromMultipleOrganizations()
    {
        await SeedAsync();

        var requests = (await _service.GetAllAsync(
            _donorId,
            UserRole.Donor,
            pageSize: 100)).ToList();

        Assert.Equal(3, requests.Count);
        Assert.Contains(
            requests,
            request =>
                request.OrganizationId == _organizationOneId);
        Assert.Contains(
            requests,
            request =>
                request.OrganizationId == _organizationTwoId);
    }

    [Fact]
    public async Task StaffOrganizationScope_IsAppliedBeforePagination()
    {
        await SeedAsync();

        var requests = (await _service.GetAllAsync(
            _staffId,
            UserRole.Staff,
            page: 2,
            pageSize: 1,
            sortBy: "createdAt",
            descending: false)).ToList();

        Assert.Single(requests);
        Assert.Equal(
            _organizationOneId,
            requests[0].OrganizationId);
    }

    private async Task SeedAsync()
    {
        _context.Organizations.AddRange(
            CreateOrganization(_organizationOneId, "Organization One"),
            CreateOrganization(_organizationTwoId, "Organization Two"));

        _context.Users.AddRange(
            CreateUser(_adminId, UserRole.Admin),
            CreateUser(_donorId, UserRole.Donor),
            CreateUser(
                _staffId,
                UserRole.Staff,
                _organizationOneId),
            CreateUser(_unassignedStaffId, UserRole.Staff));

        await _context.SaveChangesAsync();

        var createdAt = DateTime.UtcNow;
        _context.BloodRequests.AddRange(
            CreateRequest(_adminId, _organizationOneId, createdAt),
            CreateRequest(
                _adminId,
                _organizationTwoId,
                createdAt.AddSeconds(1)),
            CreateRequest(
                _adminId,
                _organizationOneId,
                createdAt.AddSeconds(2)));

        await _context.SaveChangesAsync();
    }

    private static Organization CreateOrganization(
        Guid id,
        string name)
    {
        return new Organization
        {
            Id = id,
            Name = name,
            Type = OrganizationType.Hospital,
            Address = $"{name} address",
            PhoneNumber = "000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static User CreateUser(
        Guid id,
        UserRole role,
        Guid? organizationId = null)
    {
        return new User
        {
            Id = id,
            Email = $"{id}@test.local",
            PasswordHash = "test",
            Role = role,
            FullName = role.ToString(),
            PhoneNumber = "000",
            OrganizationId = organizationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static BloodRequest CreateRequest(
        Guid requesterId,
        Guid organizationId,
        DateTime createdAt)
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
            CreatedAt = createdAt
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
