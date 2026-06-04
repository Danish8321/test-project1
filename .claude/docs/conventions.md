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

1. Validate Circle signature (HMAC/header) before processing
2. Check EventId for duplicate — store event first, then process
3. Atomic DB write: event record persisted before handler runs
4. Never mutate balances from webhook — only create ledger entries

## Logging

Every request must carry: `CorrelationId`, `CustomerId`, `FundingAccountId`

Log: Circle requests/responses, webhooks received, reconciliation results.

## Security

Never commit real API keys or secrets. `appsettings.json` is tracked with placeholder values — replace locally, never commit real values.

Config lives in `src/FundManagement.Api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ifs_poc;Username=postgres;Password=localdev"
  },
  "Circle": {
    "ApiKey": "SAND_API_KEY_HERE",
    "BaseUrl": "https://api-sandbox.circle.com",
    "WebhookSecret": "WEBHOOK_SECRET_HERE"
  }
}
```

For environment-specific overrides use `appsettings.Development.json` (gitignored).

Angular environment (`frontend/src/environments/environment.ts`):
```typescript
export const environment = { apiBaseUrl: 'http://localhost:5000' };
```

## Dapper

Raw parameterized SQL only. No query builders, no LINQ, no EF DbContext.

```csharp
// Correct
await connection.QueryAsync<Deposit>("SELECT * FROM deposits WHERE id = @Id", new { Id = id });
```