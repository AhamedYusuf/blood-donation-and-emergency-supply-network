using System.Text.Json;

namespace BloodDonationNetwork.Infrastructure.ExternalClients.Fcm;

/// <summary>The fields we need from a Firebase service-account JSON key.</summary>
public sealed record ServiceAccountKey(
    string ProjectId,
    string ClientEmail,
    string PrivateKeyPem,
    string TokenUri)
{
    public static ServiceAccountKey Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string Get(string name) =>
            root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()!
                : throw new InvalidOperationException(
                    $"Service-account key is missing '{name}'.");

        var tokenUri = root.TryGetProperty("token_uri", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()!
            : "https://oauth2.googleapis.com/token";

        return new ServiceAccountKey(
            ProjectId: Get("project_id"),
            ClientEmail: Get("client_email"),
            PrivateKeyPem: Get("private_key"),
            TokenUri: tokenUri);
    }
}
