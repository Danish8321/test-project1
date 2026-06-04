# Architecture

## Layer Diagram

```
Angular 22 (Frontend)
↓
FundManagement.Api (.NET 10 — Minimal API endpoints)
↓
FundManagement.Application (MediatR Commands/Queries/Handlers, interfaces)
↓
FundManagement.Infrastructure (Dapper + PostgreSQL, Circle HTTP client)
↓
PostgreSQL              Circle APIs (Sandbox)
```

## Clean Architecture Rules

- Dependencies point inward only
- Domain: zero external dependencies
- Application: depends only on Domain
- Infrastructure: implements Application interfaces
- Api: dispatches via MediatR only — no business logic

## CQRS Layout

```
FundManagement.Application/
  Deposits/
    Commands/CreateDeposit/{Command,Handler}.cs
    Commands/ProcessSettlement/{Command,Handler}.cs
    Queries/GetDeposit/{Query,Handler}.cs
    Queries/GetDeposits/{Query,Handler}.cs
  Withdrawals/
    Commands/CreateWithdrawal/{Command,Handler}.cs
    Commands/ProcessPayoutSettlement/{Command,Handler}.cs
    Queries/...
  Ledger/
    Commands/CreateLedgerEntry/{Command,Handler}.cs
    Queries/GetLedger/{Query,Handler}.cs
  Webhooks/
    Commands/ProcessWebhook/{Command,Handler}.cs
  Reconciliation/
    Commands/RunReconciliation/{Command,Handler}.cs
    Queries/GetReconciliationResults/{Query,Handler}.cs
  Behaviours/
    ValidationBehaviour.cs
    LoggingBehaviour.cs
```

## MediatR Registration

```csharp
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateDepositCommand).Assembly));
```

Pipeline behaviours live in `FundManagement.Application/Behaviours/`.

## Dapper DI Wiring

```csharp
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
```

Inject `IDbConnection` directly into handlers. Never create connections manually.
