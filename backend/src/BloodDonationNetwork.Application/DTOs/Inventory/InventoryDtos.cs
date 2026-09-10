using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.DTOs.Inventory;

public record InventoryResponse(
    Guid Id,
    Guid OrganizationId,
    BloodType BloodType,
    int UnitsAvailable,
    int LowStockThreshold,
    bool IsLowStock,
    DateTime LastUpdated);

public record AdjustInventoryRequest(
    int Units,
    InventoryTransactionType TransactionType,
    Guid? RelatedAppointmentId = null,
    Guid? RelatedTransferOrgId = null);

public record CreateInventoryTransactionRequest(
    Guid OrganizationId,
    BloodType BloodType,
    int Units,
    InventoryTransactionType TransactionType,
    Guid? RelatedAppointmentId = null,
    Guid? RelatedTransferOrgId = null);

public record InventoryTransactionResponse(
    Guid Id,
    Guid InventoryId,
    BloodType BloodType,
    InventoryTransactionType TransactionType,
    int Units,
    Guid? RelatedAppointmentId,
    Guid? RelatedTransferOrgId,
    DateTime CreatedAt);

public record StockCheckRequest(
    Guid OrganizationId,
    BloodType BloodType,
    int RequiredUnits);

public record StockCheckResponse(
    Guid OrganizationId,
    BloodType BloodType,
    int RequiredUnits,
    int AvailableUnits,
    int RemainingUnits,
    bool Sufficient,
    bool LowStock);

public record StockRiskResponse(
    Guid OrganizationId,
    string RiskLevel,
    int TotalUnits,
    int LowStockTypes,
    IReadOnlyList<InventoryResponse> AtRiskInventory,
    string Recommendation);

public record EmergencyInventoryRequest(
    BloodType BloodType,
    int RequiredUnits,
    RequestUrgency Urgency);

public record EmergencyInventoryRecommendation(
    BloodType BloodType,
    int RequiredUnits,
    int AvailableUnits,
    int Shortfall,
    string Recommendation,
    string SuggestedAction,
    RequestUrgency Priority);