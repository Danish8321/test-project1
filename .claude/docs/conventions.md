# Conventions & Rules

## Balance Management

Never update balances directly. Always derive from ledger entries.

```csharp
// Wrong
account.Balance += amount;

// Correct
CreateLedgerEntry(...);
RecalculateBalance(...);
```

## Ledger Is Append-Only

Never update or delete entries. Corrections = reversal entries only.

## Idempotency

Mandatory for deposits, payouts, webhooks, reconciliation jobs.
Store and check before processing:
- Deposit webhooks: `transfer.id` (Circle Mint has no `notificationId`)
- Payout webhooks: `payout.id`
- Deposit creation: `PaymentIntentId`
- Payout creation: `PayoutId`
- Recipient creation: pass a fresh `Guid.NewGuid()` as `idempotencyKey` each attempt
On duplicate: return 200, do not reprocess.

> **Recipient idempotency gap**: If `WithdrawalService.CreateAsync` is called twice for the same destination address, two separate recipients are created (different `idempotencyKey` each time). To deduplicate, derive the idempotency key from `destinationAddress` (e.g. `SHA256(customerId + destinationAddress)`). Not implemented in POC.

## Webhook Security

> Circle uses ECDSA_SHA_256 (asymmetric keys), NOT HMAC. Always verify via Circle MCP before implementing.
> Source: https://developers.circle.com/wallets/webhook-notifications
>
> Circle Mint notification format differs from Wallets API — no `subscriptionId`/`notificationId`/`version`.
> Payload uses `clientId` + `notificationType` + resource key (`payout`/`transfer`/`addressBookRecipient`).

1. Read raw body as string (properly formatted JSON) — do NOT re-serialize after parsing; whitespace matters for sig
2. Extract `X-Circle-Signature` + `X-Circle-Key-Id` headers — return 401 if missing
3. Fetch public key from `GET /v2/notifications/publicKey/{keyId}` — cache it (static per keyId)
4. Verify ECDSA-SHA256 signature over raw body — return 401 if invalid
5. Use `payout.id` or `transfer.id` as idempotency key — `ON CONFLICT DO NOTHING` (no `notificationId` in Circle Mint)
6. Store event record atomically before dispatching handler
7. Never mutate balances from webhook — only create ledger entries via `LedgerService`
8. Extract resource from top-level key matching `notificationType` (e.g. `payout` object when `notificationType == "payouts"`)

**Idempotency for unknown `notificationType`**: `WebhooksController` generates a fallback key as `"{notificationType}:{clientId}"` when no known resource key is present. This prevents duplicate processing of unrecognised events without crashing.

**Deposit settlement gap**: `transfers` notification handler only settles deposits when `transfer.paymentIntentId` is present. If Circle omits that field, inbound transfers are stored (status=Received) but not dispatched to `DepositService`. Monitor `webhook_events` table for `status = 'Received'` rows with `event_type = 'transfers'` — those indicate unlinked deposits needing manual review or a polling fallback.

## Logging

Every request must carry: `CorrelationId`, `CustomerId`, `FundingAccountId`

Log: Circle requests/responses, webhooks received, reconciliation results.

## Security

Never commit real API keys or secrets. `appsettings.json` is tracked with placeholder values — replace locally, never commit real values.

Config lives in `api/src/FundManagement.Api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ifs_poc;Username=postgres;Password=localdev"
  },
  "Circle": {
    "ApiKey": "SAND_API_KEY_HERE",
    "BaseUrl": "https://api-sandbox.circle.com"
  }
}
```
Note: `WebhookSecret` removed — Circle webhook auth uses ECDSA public key fetched from Circle API, not a shared secret.

For environment-specific overrides use `appsettings.Development.json` (gitignored).

Angular environment (`client/src/environments/environment.ts`):
```typescript
export const environment = { apiBaseUrl: 'http://localhost:5000' };
```

## Dapper

Raw parameterized SQL only. No query builders, no LINQ, no EF DbContext.

```csharp
// Correct
await connection.QueryAsync<Deposit>("SELECT * FROM deposits WHERE id = @Id", new { Id = id });
```