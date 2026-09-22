using System.Text.Json;
using System.Text.Json.Serialization;
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

public record StockCheckAgentRequest(
    Guid WorkflowId,
    Guid RequestingOrgId,
    string BloodType,
    int UnitsNeeded);

public record StockCheckCandidateTransferOrg(
    Guid OrganizationId,
    double DistanceKm,
    int UnitsAvailable);

public record StockCheckAgentResponse(
    bool Sufficient,
    int OwnStockUnits,
    int ShortfallUnits,
    IReadOnlyList<StockCheckCandidateTransferOrg> CandidateTransferOrgs);

public record StockRiskResponse(
    Guid OrganizationId,
    string RiskLevel,
    int TotalUnits,
    int LowStockTypes,
    IReadOnlyList<InventoryResponse> AtRiskInventory,
    string Recommendation);

public record EmergencyInventoryRequest(
    [property: JsonConverter(typeof(BloodTypeJsonConverter))]
    BloodType BloodType,
    int RequiredUnits,
    [property: JsonConverter(typeof(RequestUrgencyJsonConverter))]
    RequestUrgency Urgency);

public record EmergencyInventoryRecommendation(
    [property: JsonConverter(typeof(BloodTypeJsonConverter))]
    BloodType BloodType,
    int RequiredUnits,
    int AvailableUnits,
    int ShortfallUnits,
    string Recommendation,
    string SuggestedAction,
    [property: JsonConverter(typeof(RequestUrgencyJsonConverter))]
    RequestUrgency Urgency);

public sealed class BloodTypeJsonConverter : JsonConverter<BloodType>
{
    private static readonly Dictionary<BloodType, string> ToName = new()
    {
        [BloodType.APositive] = "A+",
        [BloodType.ANegative] = "A-",
        [BloodType.BPositive] = "B+",
        [BloodType.BNegative] = "B-",
        [BloodType.ABPositive] = "AB+",
        [BloodType.ABNegative] = "AB-",
        [BloodType.OPositive] = "O+",
        [BloodType.ONegative] = "O-"
    };

    private static readonly Dictionary<string, BloodType> FromName =
        ToName.ToDictionary(
            x => x.Value,
            x => x.Key,
            StringComparer.OrdinalIgnoreCase);

    public override BloodType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number &&
            reader.TryGetInt32(out var numericValue))
        {
            if (Enum.IsDefined(
                typeof(BloodType),
                numericValue))
            {
                return (BloodType)numericValue;
            }

            throw new JsonException(
                $"Invalid BloodType enum value: {numericValue}");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();

            if (value is not null &&
                FromName.TryGetValue(value, out var bloodType))
            {
                return bloodType;
            }
        }

        throw new JsonException(
            "BloodType must be a valid enum number or blood type string such as 'O-'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        BloodType value,
        JsonSerializerOptions options)
    {
        if (!ToName.TryGetValue(value, out var bloodType))
        {
            throw new JsonException(
                $"Invalid BloodType enum value: {value}");
        }

        writer.WriteStringValue(bloodType);
    }
}

public sealed class RequestUrgencyJsonConverter
    : JsonConverter<RequestUrgency>
{
    private static readonly Dictionary<RequestUrgency, string> ToName = new()
    {
        [RequestUrgency.Normal] = "Normal",
        [RequestUrgency.Urgent] = "Urgent",
        [RequestUrgency.Critical] = "Critical"
    };

    private static readonly Dictionary<string, RequestUrgency> FromName =
        ToName.ToDictionary(
            x => x.Value,
            x => x.Key,
            StringComparer.OrdinalIgnoreCase);

    public override RequestUrgency Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number &&
            reader.TryGetInt32(out var numericValue))
        {
            if (Enum.IsDefined(
                typeof(RequestUrgency),
                numericValue))
            {
                return (RequestUrgency)numericValue;
            }

            throw new JsonException(
                $"Invalid RequestUrgency enum value: {numericValue}");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();

            if (value is not null &&
                FromName.TryGetValue(value, out var urgency))
            {
                return urgency;
            }
        }

        throw new JsonException(
            "RequestUrgency must be a valid enum number or 'Normal', 'Urgent', or 'Critical'.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        RequestUrgency value,
        JsonSerializerOptions options)
    {
        if (!ToName.TryGetValue(value, out var urgency))
        {
            throw new JsonException(
                $"Invalid RequestUrgency enum value: {value}");
        }

        writer.WriteStringValue(urgency);
    }
}