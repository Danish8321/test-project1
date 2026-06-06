# Circle APIs

**MANDATORY: Always use Circle MCP (`mcp__circle__search_circle_documentation`) to verify endpoints, payload shapes, and field names before writing any Circle-related code. Never assume — docs change and assumptions cause silent bugs.**

Sandbox base URL: `https://api-sandbox.circle.com`

> **API naming as of Apr 2025:** Payments API → Crypto Deposits API. Payouts API → Crypto Payouts API.
> **Sep 2025:** Crypto Payouts API relaunched — all `/v1/businessAccount/payouts` and `/v1/businessAccount/wallets/addresses/recipient` endpoints replaced. See below.

## Connectivity
```
GET  /ping
GET  /v1/configuration
GET  /v1/stablecoins
GET  /v1/businessAccount/balances
```

## Deposits — Crypto Deposits API (Payment Intents)
```
POST /v1/paymentIntents
GET  /v1/paymentIntents/{id}
GET  /v1/paymentIntents
POST /v1/paymentIntents/{id}/expire
```

Supported chains for payment intents (API chain codes):
`ALGO ARB AVAX BASE ETH HBAR NEAR NOBLE OP POLY SOL XLM UNICHAIN WORLDCHAIN`

Payment intent request body:
```json
{
  "idempotencyKey": "<uuid>",
  "amount": { "amount": "100.00", "currency": "USD" },
  "settlementCurrency": "USD",
  "paymentMethods": [{ "type": "blockchain", "chain": "ETH" }]
}
```

Payment intent statuses: `created` → `pending` → `complete` / `expired` / `failed`

## Payouts — Crypto Payouts API (Sep 2025 relaunch)

> **Breaking change**: `/v1/businessAccount/payouts` and `/v1/businessAccount/wallets/addresses/recipient` are removed.
> Use the new endpoints below.

### Crypto Address Book (recipients — required before payout)
```
POST /v1/addressBook/recipients
GET  /v1/addressBook/recipients
GET  /v1/addressBook/recipients/{id}
```

Recipient request body:
```json
{
  "idempotencyKey": "<uuid>",
  "chain": "ETH",
  "address": "0x...",
  "nickname": "optional"
}
```

Supported chains for recipients:
`ALGO ARB AVAX BASE ETH HBAR NEAR NOBLE OP POLY SOL XLM`

Recipient statuses: `pending` → `active` (must be `active` before submitting payout).
Subscribe to `addressBookRecipients` notifications or poll `GET /v1/addressBook/recipients/{id}`.

**Implementation**: current code (`WithdrawalService`) creates recipient synchronously then polls up to 30s (6 × 5s) for `active`. Chain is hardcoded `ETH` — extend `CreateAsync` if multi-chain needed.

### Payouts
```
POST /v1/payouts
GET  /v1/payouts/{id}
GET  /v1/payouts
```

Payout request body (`source` optional — Circle uses primary business wallet if omitted):
```json
{
  "idempotencyKey": "<uuid>",
  "destination": { "type": "address_book", "id": "<recipientId>" },
  "amount": { "amount": "100.00", "currency": "USD" }
}
```

Payout statuses: `pending` → `complete` / `failed`
Error codes on failed: `insufficient_funds`, `transaction_denied`, `transaction_failed`, `transaction_returned`

> **Current implementation omits `source`** — defaults to Circle business wallet. Explicitly specify `source.id` (source wallet UUID) if the account has multiple source wallets.

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
1. Read raw body as string (properly formatted JSON string) — do NOT re-serialize after parsing
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

## Webhook Payload Format — Circle Mint

Circle Mint uses a **different payload format** from the Wallets API. There is no `subscriptionId`, `notificationId`, or `version` field.

```json
{
  "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
  "notificationType": "payouts",
  "payout": { ...payout object... }
}
```

```json
{
  "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
  "notificationType": "transfers",
  "transfer": { ...transfer object... }
}
```

```json
{
  "clientId": "c60d2d5b-203c-45bb-9f6e-93641d40a599",
  "notificationType": "addressBookRecipients",
  "addressBookRecipient": { ...recipient object... }
}
```

**Notification types this app handles:**

| `notificationType` | When fired | Action |
|---|---|---|
| `transfers` | Inbound blockchain transfer (deposit) status changes | Check `transfer.status`: `complete` → credit ledger; `failed` → fail deposit |
| `payouts` | Payout status changes | Check `payout.status`: `complete` → close withdrawal; `failed` → reverse debit |
| `addressBookRecipients` | Recipient `pending` → `active` | Allow payout submission |

**Payout object key fields:**
- `id` — payout UUID (idempotency key)
- `status` — `pending` / `complete` / `failed`
- `errorCode` — present when `failed`: `insufficient_funds`, `transaction_denied`, `transaction_failed`, `transaction_returned`

**Transfer object key fields:**
- `id` — transfer UUID (idempotency key)
- `status` — `pending` / `complete` / `failed`
- `source` — `{ type: "blockchain", chain: "ETH", address: "0x..." }` (originating address — customer's wallet for inbound deposits)
- `destination` — `{ type: "wallet", id: "..." }` (Circle business wallet ID, not the deposit address)
- `transactionHash` — onchain tx hash
- `paymentIntentId` — **may be present** when the transfer was created via a payment intent; used to link the transfer to a deposit. **Not confirmed in official model** — verify in sandbox.

**Idempotency**: use `payout.id` or `transfer.id` (the resource UUID) — Circle Mint has no `notificationId`. Same resource fires multiple notifications as status progresses.

### Known Gap: Deposit Settlement via Transfers

For inbound deposits, the `transfers` notification does not reliably carry a `paymentIntentId` in the documented Transfer object model. The Transfer object shows `destination.type = "wallet"` (not the payment intent deposit address), making it impossible to map back to a specific payment intent purely from the transfer payload.

**Current implementation** (`WebhookService`): checks for `transfer.paymentIntentId` — if present, settles deposit. If absent, the inbound transfer is logged but deposit is NOT settled automatically.

**Resolution options** (pick one when testing in sandbox):
1. Confirm via sandbox that Circle includes `paymentIntentId` on `transfers` notifications for payment intent deposits — if yes, current code works.
2. If Circle does NOT include it: add a `paymentIntents` notification type handler (Circle may fire a separate `paymentIntents`-type event with the intent ID and updated status).
3. Fallback: add a background polling job that checks all `Pending` deposits against `GET /v1/paymentIntents/{id}` on a schedule.

## Usage Rules

- **Circle MCP first** — search docs before any Circle implementation
- **Circle Skills** — generate integration code after MCP confirms shape
- No hardcoded API keys — use `appsettings.json` placeholders, override locally
- Idempotency keys: `payout.id` (payout webhooks), `transfer.id` (deposit webhooks), `PaymentIntentId` (deposit creation), `PayoutId` (payout creation)
- `WebhookSecret` is NOT used — Circle auth is ECDSA via public key API
- Recipient must be `active` before submitting a payout — poll or subscribe to `addressBookRecipients`