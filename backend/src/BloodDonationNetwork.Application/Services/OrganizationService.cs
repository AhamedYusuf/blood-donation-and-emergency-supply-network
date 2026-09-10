using BloodDonationNetwork.Application.DTOs.Organizations;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IApplicationDbContext _context;

    public OrganizationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OrganizationResponse>> GetAllAsync(
        CancellationToken ct)
    {
        return await _context.Organizations
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => ToResponse(o))
            .ToListAsync(ct);
    }

    public async Task<OrganizationResponse?> GetByIdAsync(
        Guid id,
        CancellationToken ct)
    {
        var organization = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return organization == null ? null : ToResponse(organization);
    }

    public async Task<OrganizationResponse> CreateAsync(
        CreateOrganizationRequest request,
        CancellationToken ct)
    {
        var type = ParseOrganizationType(request.Type);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Type = type,
            Address = request.Address.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PhoneNumber = request.PhoneNumber.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(organization);

        await _context.SaveChangesAsync(ct);

        return ToResponse(organization);
    }

    public async Task<OrganizationResponse> UpdateAsync(
        Guid id,
        UpdateOrganizationRequest request,
        CancellationToken ct)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (organization == null)
        {
            throw new KeyNotFoundException(
                $"Organization with ID '{id}' was not found.");
        }

        organization.Name = request.Name.Trim();
        organization.Type = ParseOrganizationType(request.Type);
        organization.Address = request.Address.Trim();
        organization.Latitude = request.Latitude;
        organization.Longitude = request.Longitude;
        organization.PhoneNumber = request.PhoneNumber.Trim();
        organization.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return ToResponse(organization);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (organization == null)
        {
            throw new KeyNotFoundException(
                $"Organization with ID '{id}' was not found.");
        }

        var hasUsers = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.OrganizationId == id, ct);

        var hasInventory = await _context.BloodBankInventories
            .AsNoTracking()
            .AnyAsync(i => i.OrganizationId == id, ct);

        if (hasUsers || hasInventory)
        {
            throw new InvalidOperationException(
                "This organization cannot be deleted because it has related users or inventory records.");
        }

        _context.Organizations.Remove(organization);

        await _context.SaveChangesAsync(ct);
    }

    private static OrganizationType ParseOrganizationType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException(
                "Organization type is required.");
        }

        if (type.Equals("hospital", StringComparison.OrdinalIgnoreCase))
        {
            return OrganizationType.Hospital;
        }

        if (type.Equals("blood_bank", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("bloodbank", StringComparison.OrdinalIgnoreCase))
        {
            return OrganizationType.BloodBank;
        }

        throw new ArgumentException(
            "Organization type must be either 'hospital' or 'blood_bank'.");
    }

    private static OrganizationResponse ToResponse(
        Organization organization)
    {
        return new OrganizationResponse
        {
            Id = organization.Id,
            Name = organization.Name,
            Type = organization.Type.ToString(),
            Address = organization.Address,
            Latitude = organization.Latitude,
            Longitude = organization.Longitude,
            PhoneNumber = organization.PhoneNumber,
            CreatedAt = organization.CreatedAt,
            UpdatedAt = organization.UpdatedAt
        };
    }
}
