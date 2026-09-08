using BloodDonationNetwork.Application.DTOs.Requests;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Services;

public class RequestService : IRequestService
{
    private readonly IApplicationDbContext _context;

    public RequestService(IApplicationDbContext context)
    {
        _context = context;
    }

    // 1. Create a new blood request
    public async Task<RequestResponseDto> CreateAsync(CreateRequestDto dto)
    {
        var request = new BloodRequest
        {
            Id = Guid.NewGuid(),
            RequesterId = dto.RequesterId,
            OrganizationId = dto.OrganizationId,
            BloodType = dto.BloodType,
            UnitsRequested = dto.UnitsRequested,
            Urgency = dto.Urgency,
            Status = BloodRequestStatus.Pending,
            HospitalName = dto.HospitalName,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        await _context.BloodRequests.AddAsync(request);
        await _context.SaveChangesAsync();

        return MapToResponse(request);
    }

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
        BloodType? bloodType = null,
        RequestUrgency? urgency = null,
        BloodRequestStatus? status = null,
        Guid? organizationId = null,
        int page = 1,
        int pageSize = 10,
        string sortBy = "createdAt",
        bool descending = true)
    {
        var query = _context.BloodRequests
            .AsNoTracking()
            .AsQueryable();

        // Filters
        if (bloodType.HasValue)
        {
            query = query.Where(r => r.BloodType == bloodType.Value);
        }

        if (urgency.HasValue)
        {
            query = query.Where(r => r.Urgency == urgency.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(r =>
                r.OrganizationId == organizationId.Value);
        }

        // Prevent invalid pagination values
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 10 : pageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

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

        return requests.Select(MapToResponse);
    }

    // 4. Update request status
    public async Task<RequestResponseDto?> UpdateStatusAsync(
        Guid id,
        RequestStatusUpdateDto dto)
    {
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return null;
        }

        request.Status = dto.Status;

        // Record fulfilment time
        if (dto.Status == BloodRequestStatus.Fulfilled)
        {
            request.FulfilledAt ??= DateTime.UtcNow;
        }

        // Record closed time
        if (dto.Status == BloodRequestStatus.Closed)
        {
            request.ClosedAt ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return MapToResponse(request);
    }

    // 5. Close a blood request
    public async Task<RequestResponseDto?> CloseAsync(Guid id)
    {
        var request = await _context.BloodRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return null;
        }

        request.Status = BloodRequestStatus.Closed;
        request.ClosedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(request);
    }

    // Convert BloodRequest entity into response DTO
    private static RequestResponseDto MapToResponse(
        BloodRequest request)
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
            ClosedAt = request.ClosedAt
        };
    }
}