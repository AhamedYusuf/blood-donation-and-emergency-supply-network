using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BloodDonationNetwork.Infrastructure.ExternalClients.Fcm;

/// <summary>
/// Mints an OAuth2 access token for the FCM scope from a service-account
/// key, via the standard JWT-bearer grant: build a JWT assertion, sign it
/// RS256 with the account's private key, and exchange it at Google's token
/// endpoint. The token is cached until shortly before it expires.
/// No external SDK — just <see cref="RSA"/> and an <see cref="HttpClient"/>.
/// </summary>
public sealed class GoogleAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly ServiceAccountKey _key;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public GoogleAccessTokenProvider(ServiceAccountKey key, HttpClient http)
    {
        _key = key;
        _http = http;
    }

    public async Task<string> GetAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _token;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _token;
            }

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = BuildSignedAssertion(),
            });

            using var response = await _http.PostAsync(_key.TokenUri, form, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google token exchange failed ({(int)response.StatusCode}): {payload}");
            }

            using var doc = JsonDocument.Parse(payload);
            _token = doc.RootElement.GetProperty("access_token").GetString()
                     ?? throw new InvalidOperationException("Token response had no access_token.");
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var e)
                ? e.GetInt32()
                : 3600;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 120);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string BuildSignedAssertion()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(
            new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = _key.ClientEmail,
            scope = Scope,
            aud = _key.TokenUri,
            iat = now,
            exp = now + 3600,
        }));

        var signingInput = $"{header}.{claims}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_key.PrivateKeyPem);
        var signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
