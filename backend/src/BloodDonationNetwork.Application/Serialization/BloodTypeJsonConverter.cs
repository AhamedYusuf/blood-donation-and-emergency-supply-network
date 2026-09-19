using System.Text.Json;
using System.Text.Json.Serialization;
using BloodDonationNetwork.Domain.Enums;

namespace BloodDonationNetwork.Application.Serialization;

public class BloodTypeJsonConverter : JsonConverter<BloodType>
{
    public override BloodType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // Backward compatibility:
        // still accept old numeric enum values.
        if (reader.TokenType == JsonTokenType.Number)
        {
            var value = reader.GetInt32();

            if (Enum.IsDefined(typeof(BloodType), value))
            {
                return (BloodType)value;
            }

            throw new JsonException(
                $"Invalid blood type value: {value}");
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(
                "Blood type must be a string.");
        }

        var valueString = reader.GetString()?.Trim();

        return valueString switch
        {
            "A+" => BloodType.APositive,
            "A-" => BloodType.ANegative,
            "B+" => BloodType.BPositive,
            "B-" => BloodType.BNegative,
            "AB+" => BloodType.ABPositive,
            "AB-" => BloodType.ABNegative,
            "O+" => BloodType.OPositive,
            "O-" => BloodType.ONegative,

            // Also accept enum-style names for compatibility.
            "APositive" => BloodType.APositive,
            "ANegative" => BloodType.ANegative,
            "BPositive" => BloodType.BPositive,
            "BNegative" => BloodType.BNegative,
            "ABPositive" => BloodType.ABPositive,
            "ABNegative" => BloodType.ABNegative,
            "OPositive" => BloodType.OPositive,
            "ONegative" => BloodType.ONegative,

            _ => throw new JsonException(
                $"Invalid blood type: '{valueString}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        BloodType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            BloodType.APositive => "A+",
            BloodType.ANegative => "A-",
            BloodType.BPositive => "B+",
            BloodType.BNegative => "B-",
            BloodType.ABPositive => "AB+",
            BloodType.ABNegative => "AB-",
            BloodType.OPositive => "O+",
            BloodType.ONegative => "O-",

            _ => throw new JsonException(
                $"Unsupported blood type: {value}")
        };

        writer.WriteStringValue(stringValue);
    }
}