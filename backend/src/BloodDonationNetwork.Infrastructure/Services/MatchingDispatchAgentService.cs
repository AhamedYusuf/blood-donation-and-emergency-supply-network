using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Agents;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Application.Services;
using BloodDonationNetwork.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Infrastructure.Services;

public class MatchingDispatchAgentService : IMatchingDispatchAgentService
{
    private readonly AppDbContext _context;
    private readonly DonorRankingCalculator _rankingCalculator;

    public MatchingDispatchAgentService(AppDbContext context, DonorRankingCalculator rankingCalculator)
    {
        _context = context;
        _rankingCalculator = rankingCalculator;
    }

    public async Task<SearchDonorsResponseDto> SearchDonorsAsync(SearchDonorsRequestDto request)
    {
        // Rough pre-filter by blood type only — real distance filtering
        // happens in-memory below, since Haversine distance can't be
        // translated into SQL by EF Core directly.
        var candidateDonors = await _context.DonorProfiles
            .Where(d => d.BloodType == request.BloodType
                     && d.VerifiedByAdmin
                     && d.Latitude != null
                     && d.Longitude != null)
            .ToListAsync();

        var candidates = new List<DonorCandidateDto>();

        foreach (var donor in candidateDonors)
        {
            var distanceKm = GeoUtils.DistanceKm(
                request.Latitude, request.Longitude,
                donor.Latitude!.Value, donor.Longitude!.Value);

            if (distanceKm > request.RadiusKm)
            {
                continue;
            }

            var score = _rankingCalculator.CalculateScore(
                distanceKm, request.UrgencyLevel, (double)donor.ReliabilityScore);

            candidates.Add(new DonorCandidateDto
            {
                DonorId = donor.Id,
                DistanceKm = Math.Round(distanceKm, 2),
                Score = Math.Round(score, 4),
                ReliabilityScore = donor.ReliabilityScore
            });
        }

        var ranked = candidates.OrderByDescending(c => c.Score).ToList();

        return new SearchDonorsResponseDto { Candidates = ranked };
    }
}