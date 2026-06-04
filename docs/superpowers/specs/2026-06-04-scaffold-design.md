# Scaffold Design — FundManagement Circle Integration POC

Date: 2026-06-04
Status: Approved

---

## Scope

Bootstrap the full project structure — .NET 10 solution, Angular 22 workspace, PostgreSQL schema, DbUp migrations, health check endpoint. No auth. No test projects. No Docker.

---

## .NET Solution Structure

```
FundManagement.sln
src/
  FundManagement.Domain/
    Entities/     Customer, FundingAccount, Deposit, Withdrawal, LedgerEntry, WebhookEvent
    Enums/        CustomerType, DepositStatus, WithdrawalStatus, EntryType, WebhookStatus
  FundManagement.Application/
    Behaviours/   LoggingBehaviour.cs
    Common/       ICircleClient.cs, IDbConnectionFactory.cs
  FundManagement.Infrastructure/
    Circle/       CircleClient.cs (HttpClient — PingAsync only at scaffold)
    Data/         DbConnectionFactory.cs (Npgsql)
    Migrations/   MigrationRunner.cs (DbUp on startup)
  FundManagement.Api/
    Program.cs    DI, Swagger, CORS
    Endpoints/    HealthEndpoint.cs
```

### NuGet Packages

| Project | Package |
|---------|---------|
| Application | MediatR |
| Infrastructure | Dapper, Npgsql, DbUp-PostgreSQL |
| Api | Swashbuckle.AspNetCore |

---

## Database Schema

### Migration Files

```
migrations/sql/
  V001__initial_schema.sql
  V002__seed_data.sql
```

### V001 — Tables

```sql
customers          (id, name, email, customer_type, created_at)
funding_accounts   (id, customer_id, currency, created_at)
deposits           (id, customer_id, funding_account_id, circle_payment_intent_id UNIQUE, amount, status, created_at, updated_at)
withdrawals        (id, customer_id, funding_account_id, circle_payout_id UNIQUE, amount, status, created_at, updated_at)
ledger_entries     (id, funding_account_id, entry_type, amount, reference_id, created_at)  -- append-only
webhook_events     (id, circle_event_id UNIQUE, event_type, payload jsonb, status, created_at, processed_at)
reconciliation_records (id, circle_reference_id, record_type, amount, status, mismatch_reason, created_at)
```

### V002 — Seed Data

2 customers: one `Circle` type, one `ExternalWallet` type. Each with one funding account (USDC).

DbUp runs on API startup. Scripts executed in `V001`, `V002` order. Already-run scripts skipped.

---

## Angular 22 Workspace

```
frontend/
  src/app/
    core/
      services/      api.service.ts
      interceptors/  correlation-id.interceptor.ts
      models/        index.ts
    features/
      dashboard/     dashboard.component.ts
      customers/     customers.component.ts
      deposits/      deposits.component.ts
      withdrawals/   withdrawals.component.ts
      ledger/        ledger.component.ts
      webhooks/      webhooks.component.ts
      reconciliation/ reconciliation.component.ts
    shared/
      components/    status-badge.component.ts, amount.component.ts
    app.routes.ts
    app.component.ts
  environments/
    environment.ts   { apiBaseUrl: 'http://localhost:5000' }
```

- All components standalone
- No NgModules
- Lazy-loaded routes per feature
- Signal stores added per feature during deposit flow (not scaffold)
- Dev server: port 4200

---

## Wiring & Health Check

### API Startup Sequence

1. Load config (appsettings + user secrets)
2. Register `IDbConnection` → `NpgsqlConnection`
3. Register `ICircleClient` → `CircleClient` (HttpClient, base URL + API key header)
4. Register MediatR from Application assembly
5. Run DbUp migrations (fail fast if DB unreachable)
6. Map endpoints + Swagger

### Health Endpoint

```
GET /health
```

```json
{
  "db": "ok",
  "circle": "ok",
  "timestamp": "2026-06-04T..."
}
```

DB check: `SELECT 1`. Circle check: `GET /ping` via CircleClient.

### CORS

Allow origin `http://localhost:4200`, all methods, all headers.

### Swagger

Available at `/swagger` in dev only.

### Config Shape

```json
{
  "Circle": {
    "ApiKey": "SAND_...",
    "BaseUrl": "https://api-sandbox.circle.com"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fund_management_poc;Username=postgres;Password=localdev"
  }
}
```

Set via `dotnet user-secrets` — never committed.

---

## Success Criteria

- `dotnet run` starts API, migrations run, no errors
- `GET /health` returns `{ db: "ok", circle: "ok" }`
- `ng serve` starts Angular app on port 4200
- All 7 routes render (empty components OK)
- Swagger UI accessible at `/swagger`
