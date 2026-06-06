using System.Text;
using System.Text.Json;
using FundManagement.Application.Common;
using Microsoft.Extensions.Logging;

namespace FundManagement.Infrastructure.Circle;

public class CircleClient : ICircleClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CircleClient> _logger;

    public CircleClient(HttpClient http, ILogger<CircleClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("/ping", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Circle ping failed");
            return false;
        }
    }

    public async Task<CirclePaymentIntentResponse> CreatePaymentIntentAsync(
        decimal amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            idempotencyKey,
            amount = new { amount = amount.ToString("F2"), currency },
            settlementCurrency = currency,
            paymentMethods = new[] { new { type = "blockchain", chain = "ETH" } }
        });

        using var response = await _http.PostAsync("/v1/paymentIntents",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePaymentIntentResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!,
            TryGetString(data, "depositAddress", "address"),
            TryGetString(data, "depositAddress", "chain"));
    }

    public async Task<CirclePaymentIntentResponse> GetPaymentIntentAsync(
        string paymentIntentId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"/v1/paymentIntents/{paymentIntentId}", ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePaymentIntentResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!,
            TryGetString(data, "depositAddress", "address"),
            TryGetString(data, "depositAddress", "chain"));
    }

    public async Task<CirclePayoutResponse> CreatePayoutAsync(
        decimal amount, string currency, string destinationAddress, string idempotencyKey, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            idempotencyKey,
            amount = new { amount = amount.ToString("F2"), currency },
            destination = new { type = "blockchain", address = destinationAddress, chain = "ETH" }
        });

        using var response = await _http.PostAsync("/v1/businessAccount/payouts",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePayoutResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!);
    }

    public async Task<CirclePayoutResponse> GetPayoutAsync(string payoutId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"/v1/businessAccount/payouts/{payoutId}", ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePayoutResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!);
    }

    private static string? TryGetString(JsonElement element, string prop1, string prop2)
    {
        if (element.TryGetProperty(prop1, out var inner) &&
            inner.TryGetProperty(prop2, out var leaf))
            return leaf.GetString();
        return null;
    }
}
