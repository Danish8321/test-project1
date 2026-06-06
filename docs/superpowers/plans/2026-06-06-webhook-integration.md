# Webhook Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `POST /webhooks/circle` to accept Circle's real webhook format, validate ECDSA-SHA256 signatures per Circle spec, and correct payload field extraction.

**Architecture:** `CircleSignatureValidator` (new, Infrastructure) owns ECDSA verification with its own `HttpClient` and a static key cache. `WebhookEndpoints` reads raw body, validates before any processing. `WebhookService` updated to use `notificationId` for idempotency and `notification.id` for resource ID.

**Tech Stack:** .NET 10 Minimal API, `System.Security.Cryptography.ECDsa`, Dapper, PostgreSQL. No MediatR.

> **Circle MCP rule:** Before touching any Circle API path or payload field, run `mcp__circle__search_circle_documentation` to confirm the current spec. Never assume field names or endpoint paths.

---

## File Map

| Action | File | Responsibility |
|---|---|---|
| Create | `api/src/FundManagement.Infrastructure/Circle/CircleSignatureValidator.cs` | ECDSA verify + public key fetch + static cache |
| Modify | `api/src/FundManagement.Api/Endpoints/WebhookEndpoints.cs` | Raw body read, header extraction, signature gate |
| Modify | `api/src/FundManagement.Infrastructure/Services/WebhookService.cs` | Fix `notificationId` usage + `notification.id` path |
| Modify | `api/src/FundManagement.Api/Program.cs` | Register `CircleSignatureValidator` via `AddHttpClient` |
| Modify | `api/src/FundManagement.Api/appsettings.json` | Remove `WebhookSecret` |

No changes to `ICircleClient`, `CircleClient`, or `IWebhookService`.

---

### Task 1: CircleSignatureValidator

**Files:**
- Create: `api/src/FundManagement.Infrastructure/Circle/CircleSignatureValidator.cs`

- [ ] **Step 1: Create `CircleSignatureValidator.cs`**

```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FundManagement.Infrastructure.Circle;

public class CircleSignatureValidator
{
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

        var doc = await JsonDocument.ParseAsync(
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
```

- [ ] **Step 2: Register in `Program.cs`**

Open `api/src/FundManagement.Api/Program.cs`. After the existing `AddHttpClient<ICircleClient, CircleClient>` block, add:

```csharp
builder.Services.AddHttpClient<CircleSignatureValidator>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Circle:BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Circle:ApiKey"]);
});
```

Add the using at the top of Program.cs:
```csharp
using FundManagement.Infrastructure.Circle;
```

- [ ] **Step 3: Verify build**

```bash
cd "C:/Users/MOGAMBO/Documents/MyDocs/Repository/Self/test-project1" && dotnet build api/FundManagement.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add api/src/FundManagement.Infrastructure/Circle/CircleSignatureValidator.cs api/src/FundManagement.Api/Program.cs
git commit -m "feat: add CircleSignatureValidator with ECDSA-SHA256 and public key cache"
```

---

### Task 2: Fix WebhookEndpoints — Raw Body + Signature Gate

**Files:**
- Modify: `api/src/FundManagement.Api/Endpoints/WebhookEndpoints.cs`

Current problem: accepts a custom `CircleWebhookRequest` DTO — Circle doesn't send that shape. No signature validation at all.

- [ ] **Step 1: Rewrite `WebhookEndpoints.cs`**

Replace the entire file:

```csharp
using System.Text;
using FundManagement.Application.Webhooks;
using FundManagement.Infrastructure.Circle;

namespace FundManagement.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/webhooks").WithTags("Webhooks");

        g.MapGet("/", async (IWebhookService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapPost("/circle", async (HttpContext ctx, CircleSignatureValidator validator, IWebhookService svc) =>
        {
            // Must read raw body — never parse+re-serialize before verifying (whitespace breaks ECDSA sig)
            ctx.Request.EnableBuffering();
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();

            var signature = ctx.Request.Headers["X-Circle-Signature"].FirstOrDefault();
            var keyId = ctx.Request.Headers["X-Circle-Key-Id"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(keyId))
                return Results.Unauthorized();

            if (!await validator.VerifyAsync(keyId, signature, rawBody))
                return Results.Unauthorized();

            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var notificationId = root.GetProperty("notificationId").GetString()!;
            var notificationType = root.GetProperty("notificationType").GetString()!;

            await svc.ProcessAsync(notificationId, notificationType, rawBody);
            return Results.Ok();
        });
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd "C:/Users/MOGAMBO/Documents/MyDocs/Repository/Self/test-project1" && dotnet build api/FundManagement.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Endpoints/WebhookEndpoints.cs
git commit -m "feat: webhook endpoint reads raw body and gates on ECDSA signature"
```

---

### Task 3: Fix WebhookService — notificationId + notification.id

**Files:**
- Modify: `api/src/FundManagement.Infrastructure/Services/WebhookService.cs`

Current problems:
1. `ProcessAsync` signature accepts `circleEventId` which was previously a custom field — now correctly driven by `notificationId` from caller (no change to signature needed, caller passes correct value after Task 2)
2. `DispatchAsync` reads `paymentIntentId` and `payoutId` at root level — Circle puts the resource ID at `notification.id`

- [ ] **Step 1: Fix `DispatchAsync` in `WebhookService.cs`**

Replace the `DispatchAsync` method (lines inside the method only — leave class structure intact):

```csharp
private async Task DispatchAsync(string eventType, string payload)
{
    using var doc = JsonDocument.Parse(payload);

    // Circle places the resource ID at notification.id — never root level
    if (!doc.RootElement.TryGetProperty("notification", out var notification))
        return;

    if (!notification.TryGetProperty("id", out var idProp))
        return;

    var resourceId = idProp.GetString();
    if (resourceId == null) return;

    switch (eventType)
    {
        case "payments.payment_intent.completed":
            await _deposits.ProcessSettlementAsync(resourceId, "complete");
            break;
        case "payments.payment_intent.failed":
            await _deposits.ProcessSettlementAsync(resourceId, "failed");
            break;
        case "payouts.payout.complete":
            await _withdrawals.ProcessPayoutSettlementAsync(resourceId, "complete");
            break;
        case "payouts.payout.failed":
            await _withdrawals.ProcessPayoutSettlementAsync(resourceId, "failed");
            break;
        // All other notificationTypes: log and ignore — do not error
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd "C:/Users/MOGAMBO/Documents/MyDocs/Repository/Self/test-project1" && dotnet build api/FundManagement.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Infrastructure/Services/WebhookService.cs
git commit -m "fix: webhook dispatch reads notification.id per Circle payload spec"
```

---

### Task 4: Remove WebhookSecret from Config

**Files:**
- Modify: `api/src/FundManagement.Api/appsettings.json`

`WebhookSecret` is not used — Circle auth is ECDSA via public key API, not a shared secret.

- [ ] **Step 1: Edit `appsettings.json`**

Replace the `Circle` block:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ifs_poc;Username=postgres;Password=localdev"
  },
  "Circle": {
    "ApiKey": "SAND_API_KEY_HERE",
    "BaseUrl": "https://api-sandbox.circle.com"
  }
}
```

- [ ] **Step 2: Verify build**

```bash
cd "C:/Users/MOGAMBO/Documents/MyDocs/Repository/Self/test-project1" && dotnet build api/FundManagement.slnx
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/appsettings.json
git commit -m "config: remove WebhookSecret — Circle uses ECDSA public key auth, not shared secret"
```

---

### Task 5: End-to-End Verification

**Goal:** Confirm the full flow works with a real Circle sandbox webhook.

- [ ] **Step 1: Start the API**

```bash
cd "C:/Users/MOGAMBO/Documents/MyDocs/Repository/Self/test-project1/api" && dotnet run --project src/FundManagement.Api
```

Expected: API starts on `http://localhost:5000`, migrations run, no errors.

- [ ] **Step 2: Start ngrok tunnel**

In a separate terminal:

```bash
ngrok http 5000
```

Copy the `https://<id>.ngrok-free.app` URL.

- [ ] **Step 3: Register webhook subscription with Circle sandbox**

> Use Circle MCP to confirm the subscriptions endpoint before running:
> `mcp__circle__search_circle_documentation` query: "notifications subscriptions endpoint"

```bash
curl -X POST https://api-sandbox.circle.com/v1/notifications/subscriptions \
  -H "Authorization: Bearer <YOUR_CIRCLE_SANDBOX_API_KEY>" \
  -H "Content-Type: application/json" \
  -d '{"endpoint": "https://<id>.ngrok-free.app/webhooks/circle"}'
```

Expected: Circle immediately sends a `webhooks.test` notification to your endpoint.

- [ ] **Step 4: Confirm test webhook received**

Check API logs for: `POST /webhooks/circle 200`

Query DB:

```sql
SELECT circle_event_id, event_type, status, created_at
FROM webhook_events
ORDER BY created_at DESC
LIMIT 5;
```

Expected: one row with `event_type = 'webhooks.test'`, `status = 'Received'` or `'Processed'`.

- [ ] **Step 5: Test invalid signature rejected**

```bash
curl -X POST http://localhost:5000/webhooks/circle \
  -H "Content-Type: application/json" \
  -H "X-Circle-Signature: invalidsig" \
  -H "X-Circle-Key-Id: 00000000-0000-0000-0000-000000000000" \
  -d '{"notificationId":"test","notificationType":"webhooks.test","notification":{},"version":2}'
```

Expected: `401 Unauthorized`

- [ ] **Step 6: Final commit**

```bash
git add -A
git commit -m "feat: Circle webhook integration complete — ECDSA validation, correct payload parsing"
```

---

## Self-Review

**Spec coverage:**
- ✓ Raw body read (never parse+re-serialize)
- ✓ `X-Circle-Signature` + `X-Circle-Key-Id` headers extracted
- ✓ 401 on missing headers
- ✓ ECDSA-SHA256 via `GET /v2/notifications/publicKey/{keyId}`
- ✓ Public key cached (static `ConcurrentDictionary`)
- ✓ 401 on invalid signature
- ✓ `notificationId` used as idempotency key (existing `ON CONFLICT DO NOTHING`)
- ✓ `notification.id` used for resource ID in dispatch
- ✓ `WebhookSecret` removed from config
- ✓ All 4 event types handled
- ✓ Unknown event types silently ignored

**Type consistency:** `CircleSignatureValidator` injected in endpoint matches registration in `Program.cs` — both use the class directly (no interface needed for single implementation).

**No placeholders:** All code blocks are complete and runnable.
