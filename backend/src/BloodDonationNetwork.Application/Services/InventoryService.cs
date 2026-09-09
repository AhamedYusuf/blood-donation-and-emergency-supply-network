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

        var newUnits = inventory.UnitsAvailable + stockChange;

        if (newUnits < 0)
        {
            throw new InvalidOperationException(
                "Insufficient inventory. Stock cannot become negative.");
        }

        inventory.UnitsAvailable = newUnits;
        inventory.LastUpdated = DateTime.UtcNow;

        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            InventoryId = inventory.Id,
            TransactionType = request.TransactionType,
            Units = request.Units,
            RelatedAppointmentId = request.RelatedAppointmentId,
            RelatedTransferOrgId = request.RelatedTransferOrgId,
            CreatedAt = DateTime.UtcNow
        };

        _db.InventoryTransactions.Add(transaction);

        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(inventory);
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
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == request.OrganizationId &&
                    x.BloodType == request.BloodType,
                cancellationToken);

        if (inventory is null)
        {
            if (request.TransactionType is
                InventoryTransactionType.UsageOut
                or InventoryTransactionType.TransferOut)
            {
                throw new KeyNotFoundException(
                    "Inventory record not found for this blood type.");
            }

            inventory = new BloodBankInventory
            {
                Id = Guid.NewGuid(),
                OrganizationId = request.OrganizationId,
                BloodType = request.BloodType,
                UnitsAvailable = 0,
                LowStockThreshold = 5,
                LastUpdated = DateTime.UtcNow
            };

            _db.BloodBankInventories.Add(inventory);
        }

        var stockChange = GetStockChange(
            request.TransactionType,
            request.Units);

        var newUnits = inventory.UnitsAvailable + stockChange;

        if (newUnits < 0)
        {
            throw new InvalidOperationException(
                "Insufficient inventory. Stock cannot become negative.");
        }

        inventory.UnitsAvailable = newUnits;
        inventory.LastUpdated = DateTime.UtcNow;

        var transaction = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            InventoryId = inventory.Id,
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
            inventory.BloodType);
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
        CancellationToken cancellationToken = default)
    {
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

    public async Task<StockRiskResponse> AnalyzeStockRiskAsync(
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