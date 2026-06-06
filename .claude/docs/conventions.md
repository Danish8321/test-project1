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
Store and check before processing: CircleEventId, PaymentIntentId, PayoutId.
On duplicate: return 200, do not reprocess.

## Webhook Security

> Circle uses ECDSA_SHA_256 (asymmetric keys), NOT HMAC. Always verify via Circle MCP before implementing.
> Source: https://developers.circle.com/wallets/webhook-notifications

1. Read raw body as string — never parse+re-serialize before verifying signature
2. Extract `X-Circle-Signature` + `X-Circle-Key-Id` headers — return 401 if missing
3. Fetch public key from `GET /v2/notifications/publicKey/{keyId}` — cache it
4. Verify ECDSA-SHA256 signature over raw body — return 401 if invalid
5. Use `notificationId` (not `EventId`) as idempotency key — `ON CONFLICT DO NOTHING`
6. Store event record atomically before dispatching handler
7. Never mutate balances from webhook — only create ledger entries via `LedgerService`
8. Extract resource ID from `notification.id` (nested), not root-level fields

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