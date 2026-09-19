using System.Text.Json;
using System.Text.Json.Serialization;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.Serialization;

public class RequestUrgencyJsonConverter
    : JsonConverter<RequestUrgency>
{
    public override RequestUrgency Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Backward compatibility with existing numeric requests.
        if (reader.TokenType == JsonTokenType.Number)
        {
            var value = reader.GetInt32();

            if (Enum.IsDefined(typeof(RequestUrgency), value))
            {
                return (RequestUrgency)value;
            }

            throw new JsonException(
                $"Invalid urgency value: {value}");
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                "Urgency must be a string.");
        }

        var valueString =
            reader.GetString()?
                .Trim()
                .ToLowerInvariant();

        return valueString switch
        {
            "normal" => RequestUrgency.Normal,

            // Internal coordinator currently refers to Normal
            // requests as "routine", so accept it as input too.
            "routine" => RequestUrgency.Normal,

            "urgent" => RequestUrgency.Urgent,
            "critical" => RequestUrgency.Critical,

            _ => throw new JsonException(
                $"Invalid request urgency: '{valueString}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        RequestUrgency value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            RequestUrgency.Normal => "normal",
            RequestUrgency.Urgent => "urgent",
            RequestUrgency.Critical => "critical",

            _ => throw new JsonException(
                $"Unsupported request urgency: {value}")
        };

        writer.WriteStringValue(stringValue);
    }
}