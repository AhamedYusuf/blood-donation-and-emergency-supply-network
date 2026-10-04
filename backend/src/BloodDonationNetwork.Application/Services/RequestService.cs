using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BloodDonationNetwork.Application.Services;

public class RequestService : IRequestService
{
    // Matches the radius matching_dispatch_agent.py defaults to when
    // searching for donors, so "nearby" means the same distance everywhere
    // in the system.
    private const double DefaultNearbyRadiusKm = 50;

    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RequestService> _logger;
    private readonly IEligibilityRuleEngine _eligibilityEngine;

    private static readonly HashSet<string> ValidRequestStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            RequestStatuses.Open,
            RequestStatuses.Matching,
            RequestStatuses.AwaitingApproval,
            RequestStatuses.DonorsNotified,
            RequestStatuses.PartiallyFulfilled,
            RequestStatuses.Fulfilled,
            RequestStatuses.Expired,
            RequestStatuses.Cancelled
        };

    public RequestService(
        IApplicationDbContext context,
        INotificationService notificationService,
        ILogger<RequestService> logger,
        IEligibilityRuleEngine? eligibilityEngine = null)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
        _eligibilityEngine = eligibilityEngine ?? new EligibilityRuleEngine();
    }

    // 1. Create a new blood request
    public async Task<RequestResponseDto> CreateAsync(
        Guid requesterId,
        Guid requestingUserId,
        bool isAdmin,
        CreateRequestDto dto)
    {
        await EnsureCanActOnOrganizationAsync(
            requestingUserId,
            isAdmin,
            dto.OrganizationId);

        var organization = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == dto.OrganizationId);

        if (organization == null)
        {
            throw new ArgumentException(
                "The selected organization was not found.");
        }

        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = requesterId,
            OrganizationId = dto.OrganizationId,
            BloodType = dto.BloodType,
            UnitsRequested = dto.UnitsRequested,
            Urgency = dto.Urgency,
            Status = RequestStatuses.Open,
            HospitalName = string.Empty,
            Latitude = organization.Latitude,
            Longitude = organization.Longitude,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        await _context.BloodRequests.AddAsync(request);
        await _context.SaveChangesAsync();

        await NotifyNearbyDonorsAsync(request);

        return MapToResponse(request);
    }

    private async Task NotifyNearbyDonorsAsync(BloodRequest request)
    {
        if (!IsValidCoordinates(request.Latitude, request.Longitude))
        {
            _logger.LogWarning(
                "Blood request {BloodRequestId} has invalid coordinates; nearby donors were not notified.",
                request.Id);
            return;
        }

        List<DonorProfile> donors;
        try
        {
            donors = await _context.DonorProfiles
                .Where(donor =>
                    donor.Latitude.HasValue &&
                    donor.Longitude.HasValue)
                .ToListAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Blood request {BloodRequestId} was created, but nearby donor profiles could not be loaded.",
                request.Id);
            return;
        }

        var data = new Dictionary<string, string>
        {
            ["type"] = "new_blood_request",
            ["bloodRequestId"] = request.Id.ToString()
        };
        var title = "New Blood Request Near You";
        var body =
            $"A new {GetBloodTypeLabel(request.BloodType)} blood request is available near you.";

        foreach (var donor in donors)
        {
            if (donor.Latitude is not { } donorLatitude ||
                donor.Longitude is not { } donorLongitude ||
                !IsValidCoordinates(donorLatitude, donorLongitude))
            {
                continue;
            }

            var distanceKm = GeoUtils.DistanceKm(
                request.Latitude,
                request.Longitude,
                donorLatitude,
                donorLongitude);

            if (!double.IsFinite(distanceKm) ||
                distanceKm > DefaultNearbyRadiusKm)
            {
                continue;
            }

            try
            {
                var result = await _notificationService.SendToDonorAsync(
                    donor.UserId,
                    title,
                    body,
                    data);

                if (!result.AnyDelivered)
                {
                    _logger.LogWarning(
                        "Nearby blood request notification for request {BloodRequestId} was not delivered to donor {DonorUserId}: {FailureReason}",
                        request.Id,
                        donor.UserId,
                        result.FailureReason ?? "No device accepted the notification.");
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Nearby blood request notification for request {BloodRequestId} failed for donor {DonorUserId}.",
                    request.Id,
                    donor.UserId);
            }
        }
    }

    private static bool IsValidCoordinates(double latitude, double longitude) =>
        double.IsFinite(latitude) &&
        latitude is >= -90 and <= 90 &&
        double.IsFinite(longitude) &&
        longitude is >= -180 and <= 180;

    private static string GetBloodTypeLabel(BloodType bloodType) =>
        bloodType switch
        {
            BloodType.APositive => BloodTypes.APositive,
            BloodType.ANegative => BloodTypes.ANegative,
            BloodType.BPositive => BloodTypes.BPositive,
            BloodType.BNegative => BloodTypes.BNegative,
            BloodType.ABPositive => BloodTypes.ABPositive,
            BloodType.ABNegative => BloodTypes.ABNegative,
            BloodType.OPositive => BloodTypes.OPositive,
            BloodType.ONegative => BloodTypes.ONegative,
            _ => throw new ArgumentOutOfRangeException(
                nameof(bloodType),
                bloodType,
                "Unknown blood type.")
        };

    // 2. Get one request by ID
    public async Task<RequestResponseDto?> GetByIdAsync(Guid id)
    {
        var request = await _context.BloodRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return null;
        }

        return MapToResponse(request);
    }

    // 3. List requests with filters, sorting and pagination
    public async Task<IEnumerable<RequestResponseDto>> GetAllAsync(
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
        var query = _context.BloodRequests
            .AsNoTracking()
            .AsQueryable();

        if (requestingUserRole == UserRole.Staff)
        {
            var staffOrganizationId =
                await _context.Users
                    .AsNoTracking()
                    .Where(user =>
                        user.Id == requestingUserId &&
                        user.Role == UserRole.Staff)
                    .Select(user => user.OrganizationId)
                    .FirstOrDefaultAsync();

            query = staffOrganizationId.HasValue
                ? query.Where(request =>
                    request.OrganizationId ==
                    staffOrganizationId.Value)
                : query.Where(_ => false);
        }

        // Filters
        if (bloodType.HasValue)
        {
            query = query.Where(
                r => r.BloodType == bloodType.Value);
        }

        if (urgency.HasValue)
        {
            query = query.Where(
                r => r.Urgency == urgency.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus =
                NormalizeAndValidateStatus(status);

            query = query.Where(
                r => r.Status == normalizedStatus);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(
                r => r.OrganizationId == organizationId.Value);
        }

        List<BloodRequest>? donorFilteredRequests = null;
        if (donorUserId is { } donorUser)
        {
            var donor = await _context.DonorProfiles
                .FirstOrDefaultAsync(profile => profile.UserId == donorUser);

            if (donor is null || !_eligibilityEngine.Evaluate(donor, donor.BloodType).IsEligible)
                return Array.Empty<RequestResponseDto>();

            var candidates = await query.ToListAsync();
            donorFilteredRequests = candidates
                .Where(request => BloodCompatibility.CanDonateTo(donor.BloodType, request.BloodType))
                .ToList();
        }

        // Prevent invalid pagination values
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var isNearbyFilter = nearLat.HasValue && nearLng.HasValue;

        // EF Core can't translate Haversine (Math.Sin/Cos) into SQL — same
        // reason MatchingDispatchAgentService computes distance in-memory
        // — so a nearby-filtered call has to materialize first, then
        // filter/sort/paginate in-memory instead of doing it all in SQL.
        if (isNearbyFilter)
        {
            var effectiveRadiusKm = radiusKm ?? DefaultNearbyRadiusKm;

            var candidates = donorFilteredRequests ?? await query.ToListAsync();

            var withDistance = candidates
                .Select(r => (
                    Request: r,
                    DistanceKm: GeoUtils.DistanceKm(
                        nearLat!.Value, nearLng!.Value,
                        r.Latitude, r.Longitude)))
                .Where(x => x.DistanceKm <= effectiveRadiusKm);

            withDistance = sortBy.ToLowerInvariant() switch
            {
                "distance" => descending
                    ? withDistance.OrderByDescending(x => x.DistanceKm)
                    : withDistance.OrderBy(x => x.DistanceKm),

                "urgency" => descending
                    ? withDistance.OrderByDescending(x => x.Request.Urgency)
                    : withDistance.OrderBy(x => x.Request.Urgency),

                "bloodtype" => descending
                    ? withDistance.OrderByDescending(x => x.Request.BloodType)
                    : withDistance.OrderBy(x => x.Request.BloodType),

                "unitsrequested" => descending
                    ? withDistance.OrderByDescending(x => x.Request.UnitsRequested)
                    : withDistance.OrderBy(x => x.Request.UnitsRequested),

                "status" => descending
                    ? withDistance.OrderByDescending(x => x.Request.Status)
                    : withDistance.OrderBy(x => x.Request.Status),

                _ => descending
                    ? withDistance.OrderByDescending(x => x.Request.CreatedAt)
                    : withDistance.OrderBy(x => x.Request.CreatedAt)
            };

            var pagedWithDistance = withDistance
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return pagedWithDistance.Select(
                x => MapToResponse(x.Request, x.DistanceKm));
        }

        if (donorFilteredRequests is not null)
        {
            IEnumerable<BloodRequest> sorted = sortBy.ToLowerInvariant() switch
            {
                "urgency" => descending
                    ? donorFilteredRequests.OrderByDescending(r => r.Urgency)
                    : donorFilteredRequests.OrderBy(r => r.Urgency),
                "bloodtype" => descending
                    ? donorFilteredRequests.OrderByDescending(r => r.BloodType)
                    : donorFilteredRequests.OrderBy(r => r.BloodType),
                "unitsrequested" => descending
                    ? donorFilteredRequests.OrderByDescending(r => r.UnitsRequested)
                    : donorFilteredRequests.OrderBy(r => r.UnitsRequested),
                _ => descending
                    ? donorFilteredRequests.OrderByDescending(r => r.CreatedAt)
                    : donorFilteredRequests.OrderBy(r => r.CreatedAt),
            };

            return sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(request => MapToResponse(request));
        }

        // Sorting
        query = sortBy.ToLowerInvariant() switch
        {
            "urgency" => descending
                ? query.OrderByDescending(r => r.Urgency)
                : query.OrderBy(r => r.Urgency),

            "bloodtype" => descending
                ? query.OrderByDescending(r => r.BloodType)
                : query.OrderBy(r => r.BloodType),

            "unitsrequested" => descending
                ? query.OrderByDescending(r => r.UnitsRequested)
                : query.OrderBy(r => r.UnitsRequested),

            "status" => descending
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),

            _ => descending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt)
        };

        // Pagination
        var requests = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return requests.Select(r => MapToResponse(r));
    }

    // 4. Update request status
    public async Task<RequestResponseDto?> UpdateStatusAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin,
        RequestStatusUpdateDto dto)
    {
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return null;
        }

        await EnsureCanActOnOrganizationAsync(
            requestingUserId,
            isAdmin,
            request.OrganizationId);

        var normalizedStatus =
            NormalizeAndValidateStatus(dto.Status);

        request.Status = normalizedStatus;

        // Record fulfilment time
        if (normalizedStatus == RequestStatuses.Fulfilled)
        {
            request.FulfilledAt ??= DateTime.UtcNow;
        }

        // The old system used a "closed" state.
        // The current specification uses "cancelled" instead.
        if (normalizedStatus == RequestStatuses.Cancelled)
        {
            request.ClosedAt ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return MapToResponse(request);
    }

    // 5. Delete a blood request
    public async Task<bool> DeleteAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin)
    {
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return false;
        }

        await EnsureCanActOnOrganizationAsync(
            requestingUserId,
            isAdmin,
            request.OrganizationId);

        _context.BloodRequests.Remove(request);
        await _context.SaveChangesAsync();

        return true;
    }

    // 6. Close a blood request
    public async Task<RequestResponseDto?> CloseAsync(
        Guid id,
        Guid requestingUserId,
        bool isAdmin)
    {
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return null;
        }

        await EnsureCanActOnOrganizationAsync(
            requestingUserId,
            isAdmin,
            request.OrganizationId);

        // "Closed" is not part of the specification's request states.
        // Existing close behavior now maps to the valid terminal
        // "cancelled" state.
        request.Status = RequestStatuses.Cancelled;
        request.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(request);
    }

    // Staff may only act on their own organization's requests.
    // Admins may act on any organization.
    private async Task EnsureCanActOnOrganizationAsync(
        Guid requestingUserId,
        bool isAdmin,
        Guid organizationId)
    {
        if (isAdmin)
        {
            return;
        }

        var requestingUser = await _context.Users
            .FirstOrDefaultAsync(
                u => u.Id == requestingUserId);

        if (requestingUser?.OrganizationId != organizationId)
        {
            throw new UnauthorizedAccessException(
                "Staff may only manage blood requests for their own organization.");
        }
    }

    private static string NormalizeAndValidateStatus(
        string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException(
                "Blood request status is required.");
        }

        var normalizedStatus =
            status.Trim().ToLowerInvariant();

        if (!ValidRequestStatuses.Contains(normalizedStatus))
        {
            throw new ArgumentException(
                $"Invalid blood request status: '{status}'. " +
                "Allowed values are: " +
                $"{RequestStatuses.Open}, " +
                $"{RequestStatuses.Matching}, " +
                $"{RequestStatuses.AwaitingApproval}, " +
                $"{RequestStatuses.DonorsNotified}, " +
                $"{RequestStatuses.PartiallyFulfilled}, " +
                $"{RequestStatuses.Fulfilled}, " +
                $"{RequestStatuses.Expired}, " +
                $"{RequestStatuses.Cancelled}.");
        }

        return normalizedStatus;
    }

    // Convert BloodRequest entity into response DTO
    private static RequestResponseDto MapToResponse(
        BloodRequest request,
        double? distanceKm = null)
    {
        return new RequestResponseDto
        {
            Id = request.Id,
            RequesterId = request.RequesterId,
            OrganizationId = request.OrganizationId,
            BloodType = request.BloodType,
            UnitsRequested = request.UnitsRequested,
            Urgency = request.Urgency,
            Status = request.Status,
            HospitalName = request.HospitalName,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Notes = request.Notes,
            CreatedAt = request.CreatedAt,
            FulfilledAt = request.FulfilledAt,
            ClosedAt = request.ClosedAt,
            DistanceKm = distanceKm
        };
    }
}