# Circle APIs

**MANDATORY: Always use Circle MCP (`mcp__circle__search_circle_documentation`) to verify endpoints, payload shapes, and field names before writing any Circle-related code. Never assume — docs change and assumptions cause silent bugs.**

Sandbox base URL: `https://api-sandbox.circle.com`

## Connectivity
```
GET  /ping
GET  /v1/configuration
GET  /v1/stablecoins
GET  /v1/businessAccount/balances
```

## Deposits (Payment Intents)
```
POST /v1/paymentIntents
GET  /v1/paymentIntents/{id}
GET  /v1/paymentIntents
```

## Payouts
```
POST /v1/businessAccount/wallets/addresses/recipient
GET  /v1/businessAccount/wallets/addresses/recipient
POST /v1/businessAccount/payouts
GET  /v1/businessAccount/payouts/{id}
GET  /v1/businessAccount/payouts
```

## Webhooks
```
POST   /v1/notifications/subscriptions
GET    /v1/notifications/subscriptions
DELETE /v1/notifications/subscriptions/{id}
```

## Webhook Signature Validation

> Source: https://developers.circle.com/wallets/webhook-notifications
> Always verify via Circle MCP before implementing — do not assume HMAC.

Circle uses **ECDSA_SHA_256** (asymmetric), NOT HMAC.

**Headers on every webhook:**
```
X-Circle-Signature: <base64-encoded ECDSA signature>
X-Circle-Key-Id:    <UUID of signing public key>
```

**Validation flow:**
1. Read raw body as string — never parse+re-serialize (whitespace breaks sig)
2. `GET /v2/notifications/publicKey/{keyId}` → `data.publicKey` (base64 DER SPKI)
3. Cache the public key — it is static per `keyId`
4. Verify: ECDSA-SHA256 over raw body bytes vs base64-decoded signature

**Public key endpoint:**
```
GET /v2/notifications/publicKey/{keyId}
```
Response:
```json
{ "data": { "id": "uuid", "algorithm": "ECDSA_SHA_256", "publicKey": "<base64 DER>" } }
```

**Circle sender IP allowlist:**
```
3.230.111.7
3.90.127.28
35.169.154.32
54.88.227.75
```

## Webhook Payload Format (version 2)

```json
{
  "subscriptionId": "uuid",
  "notificationId": "uuid",
  "notificationType": "payments.payment_intent.completed",
  "notification": {
    "id": "<circle-resource-id>",
    "status": "...",
    "..."
  },
  "timestamp": "ISO8601",
  "version": 2
}
```

- `notificationId` = idempotency key (same on retries)
- `notificationType` = event type
- `notification.id` = the Circle resource (paymentIntentId or payoutId)

**Event types this app handles:**
```
payments.payment_intent.completed  → credit deposit
payments.payment_intent.failed     → fail deposit
payouts.payout.complete            → complete withdrawal
payouts.payout.failed              → fail + reverse ledger debit
```

## Usage Rules

- **Circle MCP first** — search docs before any Circle implementation
- **Circle Skills** — generate integration code after MCP confirms shape
- No hardcoded API keys — use `appsettings.json` placeholders, override locally
- Store for idempotency: `notificationId` (webhooks), `PaymentIntentId` (deposits), `PayoutId` (withdrawals)
- `WebhookSecret` is NOT used — Circle auth is ECDSA via public key API