using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace FundManagement.Infrastructure.Circle;

public class CircleSignatureValidator
{
    // In-process ECDsa cache — ECDsa is not serializable so each instance keeps its own.
    // Redis stores the raw base64 DER so all instances share the fetched key without
    // hitting the Circle API on every startup.
    private static readonly ConcurrentDictionary<string, ECDsa> _localKeyCache = new();

    private static readonly DistributedCacheEntryOptions KeyCacheTtl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) };

    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CircleSignatureValidator> _logger;

    public CircleSignatureValidator(HttpClient http, IDistributedCache cache, ILogger<CircleSignatureValidator> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string keyId, string signature, string rawBody, CancellationToken ct = default)
    {
        using var _ = LogContext.PushProperty("CircleKeyId", keyId);
        try
        {
            var ecDsa = await GetOrFetchKeyAsync(keyId, ct);
            var sigBytes = Convert.FromBase64String(signature);
            var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
            var valid = ecDsa.VerifyData(bodyBytes, sigBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            if (!valid)
                _logger.LogWarning("Circle ECDSA signature verification failed. KeyId={KeyId}", keyId);
            return valid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Circle signature verification threw. KeyId={KeyId}", keyId);
            return false;
        }
    }

    private async Task<ECDsa> GetOrFetchKeyAsync(string keyId, CancellationToken ct)
    {
        // 1. In-process hit (fastest — avoids all I/O)
        if (_localKeyCache.TryGetValue(keyId, out var local))
            return local;

        // 2. Redis hit (shared across instances — avoids Circle API call)
        var redisKey = $"circle:pubkey:{keyId}";
        var storedBase64 = await _cache.GetStringAsync(redisKey, ct);
        if (storedBase64 != null)
        {
            _logger.LogDebug("Circle public key loaded from Redis. KeyId={KeyId}", keyId);
            return ImportAndCache(keyId, storedBase64);
        }

        // 3. Fetch from Circle API
        _logger.LogInformation("Fetching Circle public key from API. KeyId={KeyId}", keyId);
        using var response = await _http.GetAsync($"/v2/notifications/publicKey/{keyId}", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var publicKeyBase64 = doc.RootElement
            .GetProperty("data")
            .GetProperty("publicKey")
            .GetString()!;

        // 4. Write to Redis (30-day TTL — Circle keys are long-lived)
        await _cache.SetStringAsync(redisKey, publicKeyBase64, KeyCacheTtl, ct);

        return ImportAndCache(keyId, publicKeyBase64);
    }

    private static ECDsa ImportAndCache(string keyId, string publicKeyBase64)
    {
        var ecDsa = ECDsa.Create();
        ecDsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        _localKeyCache.TryAdd(keyId, ecDsa);
        return ecDsa;
    }
}
