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

1. Validate Circle signature (HMAC/header) before any processing
2. Check EventId for duplicate — store event first, then process
3. Atomic DB write: event record persisted before handler runs
4. Never mutate balances directly from webhook — only create ledger entries

## Logging

Every request must carry: `CorrelationId`, `CustomerId`, `FundingAccountId`

Log: Circle requests/responses, webhooks received, reconciliation results.

## Security

Never commit: Circle API keys, secrets, connection strings.

Use dotnet user-secrets for local dev:
```bash
cd src/FundManagement.Api
dotnet user-secrets set "Circle:ApiKey" "SAND_..."
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..."
```

Required env keys:
```json
{
  "Circle": { "ApiKey": "SAND_...", "BaseUrl": "https://api-sandbox.circle.com", "WebhookSecret": "..." },
  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=ifs_poc;Username=postgres;Password=localdev" }
}
```

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
