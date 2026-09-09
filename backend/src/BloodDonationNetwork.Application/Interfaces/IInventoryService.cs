using BloodDonationNetwork.Application.Common;
using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryResponse>> GetInventoryAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryResponse>> GetLowStockAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<InventoryResponse> AdjustInventoryAsync(
        Guid inventoryId,
        AdjustInventoryRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<InventoryTransactionResponse> CreateTransactionAsync(
        CreateInventoryTransactionRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryTransactionResponse>> GetTransactionsAsync(
        Guid organizationId,
        Guid currentUserId,
        InventoryTransactionType? type = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<StockCheckResponse> CheckStockAsync(
        StockCheckRequest request,
        CancellationToken cancellationToken = default);

    Task<StockRiskResponse> AnalyzeStockRiskAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<EmergencyInventoryRecommendation> GetEmergencyRecommendationAsync(
        EmergencyInventoryRequest request,
        CancellationToken cancellationToken = default);
}