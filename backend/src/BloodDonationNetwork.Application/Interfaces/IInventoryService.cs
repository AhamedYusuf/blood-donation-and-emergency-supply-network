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
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<StockCheckAgentResponse> CheckStockForAgentAsync(
        StockCheckAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts <paramref name="request"/>.UnitsNeeded from the requesting
    /// org's own stock and marks the related BloodRequest fulfilled. Only
    /// valid for an approved workflow (same guard Mode 2 dispatch
    /// enforces) — throws <see cref="InvalidOperationException"/>
    /// otherwise, and if the live stock can no longer cover the request
    /// (it can change between the original check-stock call and human
    /// approval).
    /// </summary>
    Task<FulfillFromStockResponse> FulfillFromStockAsync(
        StockCheckAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<StockCheckAgentResponse> CheckStockWithTransferCandidatesAsync(
        StockCheckRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<StockRiskResponse> AnalyzeStockRiskAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<StockRiskResponse> AnalyzeStockRiskForAgentAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<EmergencyInventoryRecommendation> GetEmergencyRecommendationAsync(
        EmergencyInventoryRequest request,
        CancellationToken cancellationToken = default);
}