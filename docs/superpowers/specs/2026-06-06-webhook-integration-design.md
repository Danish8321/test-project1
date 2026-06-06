# Webhook Integration Design — Circle ECDSA (Approach B)

> Verified against Circle official docs via Circle MCP. No assumptions — all behaviour sourced from `developers.circle.com`.

---

## Goal

Fix `POST /webhooks/circle` to accept Circle's actual webhook format, validate signatures using Circle's ECDSA scheme, correct payload field extraction, and update all relevant docs to reflect Circle's standard.

---

## What Circle Actually Sends

### Headers

```
X-Circle-Signature: <base64-encoded ECDSA signature>
X-Circle-Key-Id: <UUID of the public key used to sign>
```

### Payload (version 2)

```json
{
  "subscriptionId": "uuid",
  "notificationId": "uuid",
  "notificationType": "payments.payment_intent.completed",
  "notification": {
    "id": "<paymentIntentId or payoutId>",
    ...
  },
  "timestamp": "2024-01-26T18:22:19.779834211Z",
  "version": 2
}
```

- `notificationId` — idempotency key (same value on retries)
- `notificationType` — event type string
- `notification.id` — the Circle resource ID (payment intent or payout)

### Signature Validation (ECDSA, not HMAC)

1. Extract `X-Circle-Signature` + `X-Circle-Key-Id` from headers
2. Fetch public key: `GET /v2/notifications/publicKey/{keyId}` using Circle API key
   - Response: `{ data: { id, algorithm: "ECDSA_SHA_256", publicKey: "<base64 DER SPKI>" } }`
   - **Cache the key** — it is static per `keyId`, never changes
3. Verify: ECDSA-SHA256 over the **raw body string** (never parse + re-serialize)
4. `WebhookSecret` in config is **not used** — remove it

> Source: https://developers.circle.com/wallets/webhook-notifications

### Circle IP Allowlist (optional but recommended)

Circle sends webhooks only from:
- `3.230.111.7`
- `3.90.127.28`
- `35.169.154.32`
- `54.88.227.75`

> Source: https://developers.circle.com/stablefx/howtos/verify-webhook-signatures

---

## What's Wrong Today

| Current code | Correct behaviour |
|---|---|
| Accepts `CircleWebhookRequest(EventId, EventType, Payload)` custom DTO | Must read raw body + validate headers |
| Uses `WebhookSecret` for HMAC | No HMAC — use ECDSA via public key API |
| `circleEventId` = custom `EventId` field | Must use `notificationId` |
| `DispatchAsync` reads `paymentIntentId` at root | Must read `notification.id` |
| `DispatchAsync` reads `payoutId` at root | Must read `notification.id` |

---

## Files Changed

### New file: `CircleSignatureValidator.cs` (Infrastructure/Circle/)

Single responsibility: fetch public key from Circle API, cache it, verify ECDSA-SHA256.

```
CircleSignatureValidator
  - _cache: Dictionary<string, ECDsa>
  - _http: HttpClient (Circle base URL + API key)
  VerifyAsync(keyId, signature, rawBody) → bool
    1. Check cache for keyId
    2. If miss: GET /v2/notifications/publicKey/{keyId}
               parse base64 DER → ECDsa.ImportSubjectPublicKeyInfo
               store in cache
    3. ECDSA.VerifyData(UTF8(rawBody), Base64(signature), SHA256, Rfc3279DerSequence)
```

### Modified: `WebhookEndpoints.cs`

```
POST /webhooks/circle
  - Read raw body as string (HttpContext.Request → StreamReader)
  - Extract X-Circle-Signature, X-Circle-Key-Id headers → 401 if missing
  - CircleSignatureValidator.VerifyAsync → 401 if invalid
  - Parse JSON: notificationId, notificationType, notification
  - WebhookService.ProcessAsync(notificationId, notificationType, rawBody)
  - Return 200
```

Remove `CircleWebhookRequest` record.

### Modified: `WebhookService.ProcessAsync`

- Change `circleEventId` parameter source: use `notificationId` from parsed JSON

### Modified: `WebhookService.DispatchAsync`

Fix field extraction:

```csharp
// Payment intent events → notification.id
doc.RootElement
  .GetProperty("notification")
  .GetProperty("id")
  .GetString()

// Payout events → same path: notification.id
```

### Modified: `ICircleClient.cs`

Add:
```csharp
Task<string> GetPublicKeyAsync(string keyId, CancellationToken ct = default);
```

### Modified: `CircleClient.cs`

Implement `GetPublicKeyAsync`:
- `GET /v2/notifications/publicKey/{keyId}`
- Return `data.publicKey` (base64 DER string)

### Modified: `appsettings.json`

Remove `WebhookSecret`. Not used — validation is via Circle public key API.

---

## Event Types Handled

| `notificationType` | Action |
|---|---|
| `payments.payment_intent.completed` | `DepositService.ProcessSettlementAsync(id, "complete")` |
| `payments.payment_intent.failed` | `DepositService.ProcessSettlementAsync(id, "failed")` |
| `payouts.payout.complete` | `WithdrawalService.ProcessPayoutSettlementAsync(id, "complete")` |
| `payouts.payout.failed` | `WithdrawalService.ProcessPayoutSettlementAsync(id, "failed")` |

All others: log and ignore (no error).

---

## Idempotency

Unchanged — `ON CONFLICT (circle_event_id) DO NOTHING` in `webhook_events` table.
`circleEventId` column now stores `notificationId` from Circle payload.

---

## Testing with ngrok

```bash
# Terminal 1 — start API
cd api && dotnet run --project src/FundManagement.Api

# Terminal 2 — start tunnel
ngrok http 5000

# Register subscription with Circle (one-time per ngrok session)
curl -X POST https://api-sandbox.circle.com/v1/notifications/subscriptions \
  -H "Authorization: Bearer <CIRCLE_API_KEY>" \
  -H "Content-Type: application/json" \
  -d '{"endpoint": "https://<ngrok-subdomain>.ngrok.io/webhooks/circle"}'

# Verify: Circle sends a test webhook immediately on subscription
# → check webhook_events table for notificationType = "webhooks.test"
```

> Always use Circle MCP (`mcp__circle__search_circle_documentation`) to verify endpoint paths and payload shapes before implementation. Never assume field names.

---

## Doc Updates

| File | Update |
|---|---|
| `.claude/docs/circle-apis.md` | Add public key endpoint, IP allowlist, signature headers |
| `.claude/docs/conventions.md` | Correct webhook security section (ECDSA, not HMAC/secret) |
| `CLAUDE.md` | Add Teaching Mode section |
