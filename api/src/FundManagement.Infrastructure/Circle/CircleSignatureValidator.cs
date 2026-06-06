using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FundManagement.Infrastructure.Circle;

public class CircleSignatureValidator
{
    // ECDsa instances cached for app lifetime — static cache, intentional no-dispose for POC
    private static readonly ConcurrentDictionary<string, ECDsa> _keyCache = new();
    private readonly HttpClient _http;

    public CircleSignatureValidator(HttpClient http)
    {
        _http = http;
    }

    public async Task<bool> VerifyAsync(string keyId, string signature, string rawBody, CancellationToken ct = default)
    {
        try
        {
            var ecDsa = await GetOrFetchKeyAsync(keyId, ct);
            var sigBytes = Convert.FromBase64String(signature);
            var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
            return ecDsa.VerifyData(bodyBytes, sigBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch
        {
            return false;
        }
    }

    private async Task<ECDsa> GetOrFetchKeyAsync(string keyId, CancellationToken ct)
    {
        if (_keyCache.TryGetValue(keyId, out var cached))
            return cached;

        using var response = await _http.GetAsync($"/v2/notifications/publicKey/{keyId}", ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var publicKeyBase64 = doc.RootElement
            .GetProperty("data")
            .GetProperty("publicKey")
            .GetString()!;

        var ecDsa = ECDsa.Create();
        ecDsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);

        _keyCache.TryAdd(keyId, ecDsa);
        return ecDsa;
    }
}
