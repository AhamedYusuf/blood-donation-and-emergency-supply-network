using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Services;

using BloodDonationNetwork.Application.DTOs.Donors;

public class DonorService : IDonorService
{
    private readonly IApplicationDbContext _db;
    private readonly IGeocodingClient _geocoder;
    private readonly IEligibilityRuleEngine _eligibilityEngine;

    public DonorService(IApplicationDbContext db, IGeocodingClient geocoder, IEligibilityRuleEngine eligibilityEngine)
    {
        _db = db;
        _geocoder = geocoder;
        _eligibilityEngine = eligibilityEngine;
    }

    public async Task<DonorProfileResponse> RegisterAsync(Guid userId, DonorRegisterRequest request, CancellationToken ct)
    {
         var alreadyExists = await _db.DonorProfiles.AnyAsync(d => d.UserId == userId, ct);
    if (alreadyExists)
        throw new InvalidOperationException("A donor profile already exists for this user.");
        
        var profile = new DonorProfile
        {
            UserId = userId,
            BloodType = request.BloodType,
            DateOfBirth = request.DateOfBirth,
            Address = request.Address,
            MedicalFlags = request.MedicalFlags ?? new()
        };

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            var coords = await _geocoder.GeocodeAsync(request.Address, ct);
            if (coords is { } c) { profile.Latitude = c.Item1; profile.Longitude = c.Item2; profile.LocationVerified = true; }
        }

        _db.DonorProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return ToResponse(profile);
    }

    public async Task<DonorProfileResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var profile = await _db.DonorProfiles.FindAsync(new object[] { id }, ct);
        return profile is null ? null : ToResponse(profile);
    }

    public async Task<DonorProfileResponse> UpdateAsync(Guid id, DonorUpdateRequest request, CancellationToken ct)
    {
        var profile = await _db.DonorProfiles.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Donor profile not found");

        var addressChanged = request.Address is not null && request.Address != profile.Address;
        if (request.Address is not null) profile.Address = request.Address;
        if (request.MedicalFlags is not null) profile.MedicalFlags = request.MedicalFlags;
        if (request.LastDonationDate is not null) profile.LastDonationDate = request.LastDonationDate;

        if (addressChanged)
        {
            var coords = await _geocoder.GeocodeAsync(profile.Address!, ct);
            profile.LocationVerified = coords is not null;
            if (coords is { } c) { profile.Latitude = c.Item1; profile.Longitude = c.Item2; }
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToResponse(profile);
    }

    public async Task<PagedResult<DonorProfileResponse>> SearchAsync(string bloodType, double? lat, double? lng, double? radiusKm, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.DonorProfiles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(bloodType)) query = query.Where(d => d.BloodType == bloodType);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        // Distance filtering (lat/lng/radiusKm) needs Student 3's GeoUtils Haversine helper —
        // wire that in once their branch merges into development; for now this is type-filtered only.

        return new PagedResult<DonorProfileResponse> { Items = items.Select(ToResponse).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task VerifyAsync(Guid id, CancellationToken ct)
    {
        var profile = await _db.DonorProfiles.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Donor profile not found");
        profile.VerifiedByAdmin = true;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EligibilityResponse> GetEligibilityAsync(Guid id, CancellationToken ct)
    {
    var profile = await _db.DonorProfiles.FindAsync(new object[] { id }, ct)
        ?? throw new KeyNotFoundException("Donor profile not found");

    var (isEligible, reason) = _eligibilityEngine.Evaluate(profile, profile.BloodType);

    int? daysUntil = profile.LastDonationDate is { } last
        ? Math.Max(0, 90 - (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - last.DayNumber))
        : null;

    return new EligibilityResponse { IsEligible = isEligible, Reason = reason, DaysUntilEligible = daysUntil };
    }

    private static DonorProfileResponse ToResponse(DonorProfile p) => new()
    {
        Id = p.Id, UserId = p.UserId, BloodType = p.BloodType, EligibilityStatus = p.EligibilityStatus,
        DateOfBirth = p.DateOfBirth, LastDonationDate = p.LastDonationDate, Address = p.Address,
        Latitude = p.Latitude, Longitude = p.Longitude, LocationVerified = p.LocationVerified, VerifiedByAdmin = p.VerifiedByAdmin
    };
}