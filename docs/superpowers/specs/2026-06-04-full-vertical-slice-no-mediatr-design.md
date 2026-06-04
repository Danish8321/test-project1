# Full Vertical Slice — No MediatR Design

**Date:** 2026-06-04  
**Scope:** Migrations + all 6 feature services + API endpoints; remove MediatR throughout

---

## 1. Remove MediatR

- Remove `MediatR` and `MediatR.Extensions.Microsoft.DependencyInjection` packages from all `.csproj` files that reference them
- Delete `FundManagement.Application/Behaviours/` folder (contains `LoggingBehaviour.cs`)
- Remove MediatR registration block from `FundManagement.Api/Program.cs`
- `IDbConnectionFactory` and `ICircleClient` interfaces remain unchanged

---

## 2. DB Migrations (DbUp SQL scripts)

Six migration scripts under `migrations/`, numbered sequentially.

```sql
-- V001__customers.sql
CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    customer_type TEXT NOT NULL,  -- 'Circle' | 'ExternalWallet'
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- V002__funding_accounts.sql
CREATE TABLE funding_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    currency TEXT NOT NULL DEFAULT 'USD',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- No balance column. Balance always derived from ledger_entries.

-- V003__deposits.sql
CREATE TABLE deposits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    circle_payment_intent_id TEXT,
    amount NUMERIC(18,6) NOT NULL,
    currency TEXT NOT NULL DEFAULT 'USD',
    status TEXT NOT NULL,  -- 'Pending' | 'Settled' | 'Failed'
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- V004__withdrawals.sql
CREATE TABLE withdrawals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    circle_payout_id TEXT,
    amount NUMERIC(18,6) NOT NULL,
    currency TEXT NOT NULL DEFAULT 'USD',
    status TEXT NOT NULL,  -- 'Pending' | 'Settled' | 'Failed'
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- V005__ledger_entries.sql
CREATE TABLE ledger_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    entry_type TEXT NOT NULL,  -- 'Credit' | 'Debit'
    amount NUMERIC(18,6) NOT NULL,
    reference_id TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- Append-only. Never update or delete. Corrections = reversal entries.

-- V006__webhook_events.sql
CREATE TABLE webhook_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id TEXT NOT NULL UNIQUE,  -- Circle's id, used for idempotency
    event_type TEXT NOT NULL,
    payload JSONB NOT NULL,
    status TEXT NOT NULL,  -- 'Pending' | 'Processed' | 'Failed'
    processed_at TIMESTAMPTZ
);
```

---

## 3. Application Layer — Service Interfaces

No CQRS folders. Each feature gets one interface file.

```
FundManagement.Application/
  Common/
    IDbConnectionFactory.cs     (unchanged)
    ICircleClient.cs            (unchanged)
  Customers/
    ICustomerService.cs
  Deposits/
    IDepositService.cs
  Withdrawals/
    IWithdrawalService.cs
  Ledger/
    ILedgerService.cs
  Webhooks/
    IWebhookService.cs
  Reconciliation/
    IReconciliationService.cs
```

### Interface Methods

**ICustomerService**
```csharp
Task<IEnumerable<Customer>> GetAllAsync();
Task<Customer?> GetByIdAsync(Guid id);
Task<Customer> CreateAsync(string name, string email, CustomerType type);
Task<IEnumerable<FundingAccount>> GetFundingAccountsAsync(Guid customerId);
```

**IDepositService**
```csharp
Task<IEnumerable<Deposit>> GetAllAsync();
Task<IEnumerable<Deposit>> GetByCustomerAsync(Guid customerId);
Task<Deposit?> GetByIdAsync(Guid id);
Task<Deposit> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount, string currency);
Task ProcessSettlementAsync(Guid depositId, string circlePaymentIntentId);
```

**IWithdrawalService**
```csharp
Task<IEnumerable<Withdrawal>> GetAllAsync();
Task<IEnumerable<Withdrawal>> GetByCustomerAsync(Guid customerId);
Task<Withdrawal?> GetByIdAsync(Guid id);
Task<Withdrawal> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount, string currency);
Task ProcessPayoutSettlementAsync(Guid withdrawalId, string circlePayoutId);
```

**ILedgerService**
```csharp
Task<IEnumerable<LedgerEntry>> GetByFundingAccountAsync(Guid fundingAccountId);
Task<decimal> GetBalanceAsync(Guid fundingAccountId);
Task<LedgerEntry> CreateEntryAsync(Guid fundingAccountId, EntryType entryType, decimal amount, string referenceId);
```

**IWebhookService**
```csharp
Task<IEnumerable<WebhookEvent>> GetAllAsync();
Task<WebhookEvent?> GetByEventIdAsync(string eventId);
Task ProcessAsync(string eventId, string eventType, string payload);
```

**IReconciliationService**
```csharp
Task<ReconciliationResult> RunAsync();
Task<IEnumerable<ReconciliationResult>> GetResultsAsync();
```

---

## 4. Infrastructure — Service Implementations

```
FundManagement.Infrastructure/
  Services/
    CustomerService.cs
    DepositService.cs
    WithdrawalService.cs
    LedgerService.cs
    WebhookService.cs
    ReconciliationService.cs
  Circle/
    CircleClient.cs             (unchanged)
  Data/
    DbConnectionFactory.cs      (unchanged)
  Migrations/
    MigrationRunner.cs          (unchanged)
```

Each service:
- Constructor-injects `IDbConnectionFactory`
- `DepositService`, `WithdrawalService`, `ReconciliationService` also inject `ICircleClient`
- Raw parameterized Dapper SQL only — no query builders
- `LedgerService.CreateEntryAsync` called by `DepositService`/`WithdrawalService` on settlement (not direct from endpoints)
- Idempotency enforced in `WebhookService.ProcessAsync` — check `event_id` uniqueness before insert

---

## 5. API Endpoints

```
FundManagement.Api/Endpoints/
  HealthEndpoint.cs             (unchanged)
  CustomerEndpoints.cs
  DepositEndpoints.cs
  WithdrawalEndpoints.cs
  LedgerEndpoints.cs
  WebhookEndpoints.cs
  ReconciliationEndpoints.cs
```

Each file: `static class` with `MapXEndpoints(this WebApplication app)` extension method.

### Routes

| Method | Route | Handler |
|--------|-------|---------|
| GET | /customers | GetAll |
| GET | /customers/{id} | GetById |
| POST | /customers | Create |
| GET | /customers/{id}/funding-accounts | GetFundingAccounts |
| GET | /deposits | GetAll |
| GET | /deposits/{id} | GetById |
| POST | /deposits | Create |
| GET | /withdrawals | GetAll |
| GET | /withdrawals/{id} | GetById |
| POST | /withdrawals | Create |
| GET | /funding-accounts/{id}/ledger | GetLedger |
| GET | /funding-accounts/{id}/balance | GetBalance |
| GET | /webhooks | GetAll |
| POST | /webhooks/circle | ReceiveCircleWebhook |
| GET | /reconciliation | GetResults |
| POST | /reconciliation/run | RunReconciliation |

Services injected as parameters directly on each route handler delegate.

---

## 6. DI Registration (`Program.cs`)

```csharp
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IDepositService, DepositService>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
```

MediatR registration block removed entirely.

---

## 7. Success Criteria

- `dotnet build` with zero errors
- All 6 migration scripts run via `MigrationRunner` on startup
- Each endpoint returns correct HTTP status (200/201/404/409)
- Deposit create → Circle payment intent created → `circle_payment_intent_id` stored
- Webhook receive → idempotency check → ledger credit created
- Ledger balance = sum of credits − sum of debits for a funding account
- `dotnet run` starts without errors against local PostgreSQL

---

## Out of Scope

- Frontend wiring (separate session)
- Authentication / authorization
- Circle-to-Circle internal transfers
- Webhook signature validation (document only, implement later)
