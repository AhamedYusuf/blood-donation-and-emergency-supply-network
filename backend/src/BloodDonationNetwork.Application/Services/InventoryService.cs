using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _db;

    public InventoryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<InventoryResponse>> GetInventoryAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationAccessAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        var inventory = await _db.BloodBankInventories
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.BloodType)
            .ToListAsync(cancellationToken);

        return inventory.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<InventoryResponse>> GetLowStockAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationAccessAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        var inventory = await _db.BloodBankInventories
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.UnitsAvailable <= x.LowStockThreshold)
            .OrderBy(x => x.UnitsAvailable)
            .ToListAsync(cancellationToken);

        return inventory.Select(ToResponse).ToList();
    }

    public async Task<InventoryResponse> AdjustInventoryAsync(
        Guid inventoryId,
        AdjustInventoryRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateUnits(request.Units);

        var inventory = await _db.BloodBankInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == inventoryId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Inventory record not found.");

        await EnsureOrganizationAccessAsync(
            inventory.OrganizationId,
            currentUserId,
            cancellationToken);

        var stockChange = GetStockChange(
            request.TransactionType,
            request.Units);

        var updatedInventory = await ApplyStockChangeAsync(
            inventoryId,
            stockChange,
            cancellationToken);

        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            InventoryId = inventoryId,
            TransactionType = request.TransactionType,
            Units = request.Units,
            RelatedAppointmentId = request.RelatedAppointmentId,
            RelatedTransferOrgId = request.RelatedTransferOrgId,
            CreatedAt = DateTime.UtcNow
        };

        _db.InventoryTransactions.Add(transaction);

        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(updatedInventory);
    }

    // Applies a stock delta as a single atomic UPDATE instead of the
    // read-modify-write pattern this used to follow (load the tracked
    // entity, mutate UnitsAvailable in memory, SaveChangesAsync). Under
    // that pattern, two concurrent adjustments to the same inventory row
    // (e.g. two staff completing appointments for the same blood type at
    // once) can both read the same starting UnitsAvailable and both
    // "succeed," with the second SaveChangesAsync silently overwriting
    // the first — a genuine lost update, not just a theoretical race.
    // ExecuteUpdateAsync issues one UPDATE ... SET UnitsAvailable =
    // UnitsAvailable + @delta WHERE ... statement that Postgres evaluates
    // against the row's live value at write time, so the negative-stock
    // guard and the increment are checked and applied as one atomic step
    // no interleaved transaction can invalidate.
    private async Task<BloodBankInventory> ApplyStockChangeAsync(
        Guid inventoryId,
        int stockChange,
        CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow;

        var rowsAffected = await _db.BloodBankInventories
            .Where(x =>
                x.Id == inventoryId &&
                x.UnitsAvailable + stockChange >= 0)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.UnitsAvailable, x => x.UnitsAvailable + stockChange)
                    .SetProperty(x => x.LastUpdated, updatedAt),
                cancellationToken);

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException(
                "Insufficient inventory. Stock cannot become negative.");
        }

        return await _db.BloodBankInventories
            .AsNoTracking()
            .FirstAsync(x => x.Id == inventoryId, cancellationToken);
    }

    public async Task<InventoryTransactionResponse> CreateTransactionAsync(
        CreateInventoryTransactionRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateUnits(request.Units);

        await EnsureOrganizationAccessAsync(
            request.OrganizationId,
            currentUserId,
            cancellationToken);

        var inventory = await _db.BloodBankInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == request.OrganizationId &&
                    x.BloodType == request.BloodType,
                cancellationToken);

        var stockChange = GetStockChange(
            request.TransactionType,
            request.Units);

        BloodBankInventory updatedInventory;

        if (inventory is null)
        {
            if (request.TransactionType is
                InventoryTransactionType.UsageOut
                or InventoryTransactionType.TransferOut)
            {
                throw new KeyNotFoundException(
                    "Inventory record not found for this blood type.");
            }

            if (stockChange < 0)
            {
                throw new InvalidOperationException(
                    "Insufficient inventory. Stock cannot become negative.");
            }

            // No existing row to race over yet — this first-ever
            // transaction for this org/blood-type pair still creates it
            // as a normal tracked insert. (A second concurrent "first"
            // transaction for the same pair could still race here and
            // create a duplicate row; that's a narrower, separate gap —
            // a unique constraint on (OrganizationId, BloodType) would
            // close it — and isn't the lost-update race this method was
            // flagged for, which is about existing rows being
            // incremented/decremented past each other.)
            updatedInventory = new BloodBankInventory
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BloodType = request.BloodType,
                UnitsAvailable = stockChange,
                LowStockThreshold = 5,
                LastUpdated = DateTime.UtcNow
            };

            _db.BloodBankInventories.Add(updatedInventory);
        }
        else
        {
            updatedInventory = await ApplyStockChangeAsync(
                inventory.Id,
                stockChange,
                cancellationToken);
        }

        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            InventoryId = updatedInventory.Id,
            TransactionType = request.TransactionType,
            Units = request.Units,
            RelatedAppointmentId = request.RelatedAppointmentId,
            RelatedTransferOrgId = request.RelatedTransferOrgId,
            CreatedAt = DateTime.UtcNow
        };

        _db.InventoryTransactions.Add(transaction);

        await _db.SaveChangesAsync(cancellationToken);

        return ToTransactionResponse(
            transaction,
            updatedInventory.BloodType);
    }

    public async Task<PagedResult<InventoryTransactionResponse>> GetTransactionsAsync(
        Guid organizationId,
        Guid currentUserId,
        InventoryTransactionType? type = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationAccessAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.InventoryTransactions
            .AsNoTracking()
            .Include(x => x.Inventory)
            .Where(x =>
                x.Inventory.OrganizationId == organizationId);

        if (type.HasValue)
        {
            query = query.Where(
                x => x.TransactionType == type.Value);
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryTransactionResponse>
        {
            Items = items
                .Select(x =>
                    ToTransactionResponse(
                        x,
                        x.Inventory.BloodType))
                .ToList(),

            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<StockCheckResponse> CheckStockAsync(
        StockCheckRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        // Unlike GetEmergencyRecommendationAsync (a deliberate system-wide
        // aggregate with no OrganizationId in its request at all), this
        // takes a caller-supplied OrganizationId and returns that specific
        // org's exact unit count — without this check, any staff member
        // could pass a different org's GUID and read their private stock.
        await EnsureOrganizationAccessAsync(
            request.OrganizationId,
            currentUserId,
            cancellationToken);

        ValidateUnits(request.RequiredUnits);

        var inventory = await _db.BloodBankInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == request.OrganizationId &&
                    x.BloodType == request.BloodType,
                cancellationToken);

        var availableUnits = inventory?.UnitsAvailable ?? 0;

        var remainingUnits =
            availableUnits - request.RequiredUnits;

        return new StockCheckResponse(
            request.OrganizationId,
            request.BloodType,
            request.RequiredUnits,
            availableUnits,
            Math.Max(remainingUnits, 0),
            availableUnits >= request.RequiredUnits,
            inventory is not null &&
            availableUnits <= inventory.LowStockThreshold);
    }


    public async Task<StockCheckAgentResponse> CheckStockForAgentAsync(
        StockCheckAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUnits(request.UnitsNeeded);

        var bloodType = ParseBloodType(request.BloodType);

        return await CheckStockWithTransferCandidatesCoreAsync(
            request.RequestingOrgId,
            bloodType,
            request.UnitsNeeded,
            cancellationToken);
    }

    public async Task<StockCheckAgentResponse> CheckStockWithTransferCandidatesAsync(
        StockCheckRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        // Same org-ownership check CheckStockAsync uses — this is the
        // browser-facing counterpart of CheckStockForAgentAsync (which
        // trusts its caller implicitly because it sits behind the
        // internal-secret middleware, not a real user's JWT). A plain
        // staff member has no such trust boundary, so it must be enforced
        // here the same way every other org-scoped read in this class does.
        await EnsureOrganizationAccessAsync(
            request.OrganizationId,
            currentUserId,
            cancellationToken);

        ValidateUnits(request.RequiredUnits);

        return await CheckStockWithTransferCandidatesCoreAsync(
            request.OrganizationId,
            request.BloodType,
            request.RequiredUnits,
            cancellationToken);
    }

    private async Task<StockCheckAgentResponse> CheckStockWithTransferCandidatesCoreAsync(
        Guid requestingOrgId,
        BloodType bloodType,
        int unitsNeeded,
        CancellationToken cancellationToken)
    {
        var requestingOrg = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == requestingOrgId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Requesting organization was not found.");

        var ownInventory = await _db.BloodBankInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == requestingOrgId &&
                    x.BloodType == bloodType,
                cancellationToken);

        var ownStockUnits = ownInventory?.UnitsAvailable ?? 0;
        var sufficient = ownStockUnits >= unitsNeeded;
        var shortfallUnits = Math.Max(
            0,
            unitsNeeded - ownStockUnits);

        if (sufficient)
        {
            return new StockCheckAgentResponse(
                true,
                ownStockUnits,
                0,
                Array.Empty<StockCheckCandidateTransferOrg>());
        }

        var nearbyInventory = await _db.BloodBankInventories
            .AsNoTracking()
            .Include(x => x.Organization)
            .Where(x =>
                x.OrganizationId != requestingOrgId &&
                x.BloodType == bloodType &&
                x.UnitsAvailable > 0)
            .ToListAsync(cancellationToken);

        var candidates = nearbyInventory
            .Select(x => new StockCheckCandidateTransferOrg(
                x.OrganizationId,
                Math.Round(
                    GeoUtils.DistanceKm(
                        requestingOrg.Latitude,
                        requestingOrg.Longitude,
                        x.Organization.Latitude,
                        x.Organization.Longitude),
                    2),
                x.UnitsAvailable))
            .OrderBy(x => x.DistanceKm)
            .ThenByDescending(x => x.UnitsAvailable)
            .ToList();

        return new StockCheckAgentResponse(
            false,
            ownStockUnits,
            shortfallUnits,
            candidates);
    }

    private static BloodType ParseBloodType(string bloodType)
    {
        return bloodType?.Trim().ToUpperInvariant() switch
        {
            "A+" => BloodType.APositive,
            "A-" => BloodType.ANegative,
            "B+" => BloodType.BPositive,
            "B-" => BloodType.BNegative,
            "AB+" => BloodType.ABPositive,
            "AB-" => BloodType.ABNegative,
            "O+" => BloodType.OPositive,
            "O-" => BloodType.ONegative,
            _ => throw new ArgumentException(
                "BloodType must be one of A+, A-, B+, B-, AB+, AB-, O+, or O-.",
                nameof(bloodType))
        };
    }

    public async Task<StockRiskResponse> AnalyzeStockRiskAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationAccessAsync(
            organizationId,
            currentUserId,
            cancellationToken);

        return await AnalyzeStockRiskCoreAsync(
            organizationId,
            cancellationToken);
    }

    public async Task<StockRiskResponse> AnalyzeStockRiskForAgentAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await AnalyzeStockRiskCoreAsync(
            organizationId,
            cancellationToken);
    }

    private async Task<StockRiskResponse> AnalyzeStockRiskCoreAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var inventory = await _db.BloodBankInventories
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.UnitsAvailable)
            .ToListAsync(cancellationToken);

        var atRisk = inventory
            .Where(x =>
                x.UnitsAvailable <= x.LowStockThreshold)
            .Select(ToResponse)
            .ToList();

        var totalUnits = inventory.Sum(
            x => x.UnitsAvailable);

        var riskLevel = atRisk.Count switch
        {
            0 => "LOW",
            1 => "MEDIUM",
            _ => "HIGH"
        };

        var recommendation = atRisk.Count switch
        {
            0 =>
                "Inventory levels are currently healthy.",

            1 =>
                $"Monitor {atRisk[0].BloodType} and consider replenishment.",

            _ =>
                "Multiple blood types are low. Prioritize replenishment and consider emergency transfers."
        };

        return new StockRiskResponse(
            organizationId,
            riskLevel,
            totalUnits,
            atRisk.Count,
            atRisk,
            recommendation);
    }

    public async Task<EmergencyInventoryRecommendation> GetEmergencyRecommendationAsync(
        EmergencyInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUnits(request.RequiredUnits);

        var inventories = await _db.BloodBankInventories
            .AsNoTracking()
            .Where(x =>
                x.BloodType == request.BloodType)
            .ToListAsync(cancellationToken);

        var availableUnits = inventories.Sum(
            x => x.UnitsAvailable);

        var shortfall = Math.Max(
            0,
            request.RequiredUnits - availableUnits);

        string recommendation;
        string suggestedAction;

        if (shortfall == 0)
        {
            recommendation = "SUFFICIENT_STOCK";

            suggestedAction =
                request.Urgency == RequestUrgency.Critical
                    ? "RESERVE_STOCK_AND_PROCEED"
                    : "PROCEED";
        }
        else if (availableUnits > 0)
        {
            recommendation = "PARTIAL_STOCK";

            suggestedAction =
                request.Urgency == RequestUrgency.Critical
                    ? "USE_AVAILABLE_STOCK_AND_REQUEST_EMERGENCY_TRANSFER"
                    : "REQUEST_TRANSFER";
        }
        else
        {
            recommendation = "NO_STOCK";
            suggestedAction = "SEARCH_OTHER_ORGANIZATIONS";
        }

        return new EmergencyInventoryRecommendation(
            request.BloodType,
            request.RequiredUnits,
            availableUnits,
            shortfall,
            recommendation,
            suggestedAction,
            request.Urgency);
    }

    private async Task EnsureOrganizationAccessAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "Authenticated user was not found.");
        }

        if (user.Role == UserRole.Admin)
        {
            return;
        }

        if (user.Role != UserRole.Staff ||
            user.OrganizationId != organizationId)
        {
            throw new ForbiddenAccessException(
                "You do not have access to this organization's inventory.");
        }
    }

    private static int GetStockChange(
        InventoryTransactionType transactionType,
        int units)
    {
        return transactionType switch
        {
            InventoryTransactionType.DonationIn => units,

            InventoryTransactionType.TransferIn => units,

            InventoryTransactionType.UsageOut => -units,

            InventoryTransactionType.TransferOut => -units,

            _ => throw new ArgumentOutOfRangeException(
                nameof(transactionType),
                "Unsupported inventory transaction type.")
        };
    }

    private static void ValidateUnits(int units)
    {
        if (units <= 0)
        {
            throw new ArgumentException(
                "Units must be greater than zero.",
                nameof(units));
        }
    }

    private static InventoryResponse ToResponse(
        BloodBankInventory inventory)
    {
        return new InventoryResponse(
            inventory.Id,
            inventory.OrganizationId,
            inventory.BloodType,
            inventory.UnitsAvailable,
            inventory.LowStockThreshold,
            inventory.UnitsAvailable <=
                inventory.LowStockThreshold,
            inventory.LastUpdated);
    }

    private static InventoryTransactionResponse ToTransactionResponse(
        InventoryTransaction transaction,
        BloodType bloodType)
    {
        return new InventoryTransactionResponse(
            transaction.Id,
            transaction.InventoryId,
            bloodType,
            transaction.TransactionType,
            transaction.Units,
            transaction.RelatedAppointmentId,
            transaction.RelatedTransferOrgId,
            transaction.CreatedAt);
    }
}