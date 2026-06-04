# Full Vertical Slice — No MediatR Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove MediatR, add DB migrations for all 6 tables, implement 6 feature service classes, and wire up all Minimal API endpoints.

**Architecture:** Feature service interfaces in Application layer, concrete Dapper implementations in Infrastructure, services injected directly into Minimal API endpoint handlers. No CQRS bus — just plain method calls through interfaces.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Dapper, PostgreSQL 16, DbUp, Npgsql

---

## File Map

**Modified:**
- `src/FundManagement.Application/FundManagement.Application.csproj` — remove MediatR package
- `src/FundManagement.Application/Common/ICircleClient.cs` — add payment intent + payout methods
- `src/FundManagement.Infrastructure/Circle/CircleClient.cs` — implement new ICircleClient methods
- `src/FundManagement.Api/Program.cs` — remove MediatR, add service DI, map all endpoints

**Deleted:**
- `src/FundManagement.Application/Behaviours/LoggingBehaviour.cs`

**Created:**
- `src/FundManagement.Infrastructure/Migrations/Scripts/V001__customers.sql`
- `src/FundManagement.Infrastructure/Migrations/Scripts/V002__funding_accounts.sql`
- `src/FundManagement.Infrastructure/Migrations/Scripts/V003__deposits.sql`
- `src/FundManagement.Infrastructure/Migrations/Scripts/V004__withdrawals.sql`
- `src/FundManagement.Infrastructure/Migrations/Scripts/V005__ledger_entries.sql`
- `src/FundManagement.Infrastructure/Migrations/Scripts/V006__webhook_events.sql`
- `src/FundManagement.Application/Common/CircleDtos.cs`
- `src/FundManagement.Application/Reconciliation/ReconciliationResult.cs`
- `src/FundManagement.Application/Customers/ICustomerService.cs`
- `src/FundManagement.Application/Deposits/IDepositService.cs`
- `src/FundManagement.Application/Withdrawals/IWithdrawalService.cs`
- `src/FundManagement.Application/Ledger/ILedgerService.cs`
- `src/FundManagement.Application/Webhooks/IWebhookService.cs`
- `src/FundManagement.Application/Reconciliation/IReconciliationService.cs`
- `src/FundManagement.Infrastructure/Data/DapperConfig.cs`
- `src/FundManagement.Infrastructure/Services/CustomerService.cs`
- `src/FundManagement.Infrastructure/Services/LedgerService.cs`
- `src/FundManagement.Infrastructure/Services/DepositService.cs`
- `src/FundManagement.Infrastructure/Services/WithdrawalService.cs`
- `src/FundManagement.Infrastructure/Services/WebhookService.cs`
- `src/FundManagement.Infrastructure/Services/ReconciliationService.cs`
- `src/FundManagement.Api/Endpoints/CustomerEndpoints.cs`
- `src/FundManagement.Api/Endpoints/DepositEndpoints.cs`
- `src/FundManagement.Api/Endpoints/WithdrawalEndpoints.cs`
- `src/FundManagement.Api/Endpoints/LedgerEndpoints.cs`
- `src/FundManagement.Api/Endpoints/WebhookEndpoints.cs`
- `src/FundManagement.Api/Endpoints/ReconciliationEndpoints.cs`

---

## Task 1: Remove MediatR

**Files:**
- Modify: `src/FundManagement.Application/FundManagement.Application.csproj`
- Delete: `src/FundManagement.Application/Behaviours/LoggingBehaviour.cs`
- Modify: `src/FundManagement.Api/Program.cs`

- [ ] **Step 1: Remove MediatR package from Application.csproj**

Replace the entire file content:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\FundManagement.Domain\FundManagement.Domain.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [ ] **Step 2: Delete LoggingBehaviour.cs**

```bash
del "src\FundManagement.Application\Behaviours\LoggingBehaviour.cs"
rmdir "src\FundManagement.Application\Behaviours"
```

- [ ] **Step 3: Strip MediatR from Program.cs**

Replace `src/FundManagement.Api/Program.cs` with the minimal wired version (no MediatR registration, no `using MediatR`):

```csharp
using System.Net.Http.Headers;
using FundManagement.Api.Endpoints;
using FundManagement.Application.Common;
using FundManagement.Infrastructure.Circle;
using FundManagement.Infrastructure.Data;
using FundManagement.Infrastructure.Migrations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new DbConnectionFactory(connectionString));

builder.Services.AddHttpClient<ICircleClient, CircleClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Circle:BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Circle:ApiKey"]);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

new MigrationRunner(connectionString).Run();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();

app.MapHealthEndpoints();

app.Run();
```

- [ ] **Step 4: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/FundManagement.Application/FundManagement.Application.csproj
git add src/FundManagement.Api/Program.cs
git rm src/FundManagement.Application/Behaviours/LoggingBehaviour.cs
git commit -m "chore: remove MediatR — replace with direct service injection"
```

---

## Task 2: DB Migration Scripts

**Files:**
- Create: `src/FundManagement.Infrastructure/Migrations/Scripts/V001__customers.sql`
- Create: `src/FundManagement.Infrastructure/Migrations/Scripts/V002__funding_accounts.sql`
- Create: `src/FundManagement.Infrastructure/Migrations/Scripts/V003__deposits.sql`
- Create: `src/FundManagement.Infrastructure/Migrations/Scripts/V004__withdrawals.sql`
- Create: `src/FundManagement.Infrastructure/Migrations/Scripts/V005__ledger_entries.sql`
- Create: `src/FundManagement.Infrastructure/Migrations/Scripts/V006__webhook_events.sql`

> These files are embedded resources picked up by `MigrationRunner` via `WithScriptsEmbeddedInAssembly`. The csproj already has `<EmbeddedResource Include="Migrations\Scripts\*.sql" />`.

- [ ] **Step 1: Create Scripts directory and V001__customers.sql**

```bash
mkdir -p src/FundManagement.Infrastructure/Migrations/Scripts
```

`src/FundManagement.Infrastructure/Migrations/Scripts/V001__customers.sql`:
```sql
CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    customer_type TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- [ ] **Step 2: Create V002__funding_accounts.sql**

`src/FundManagement.Infrastructure/Migrations/Scripts/V002__funding_accounts.sql`:
```sql
CREATE TABLE funding_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    currency TEXT NOT NULL DEFAULT 'USDC',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- [ ] **Step 3: Create V003__deposits.sql**

`src/FundManagement.Infrastructure/Migrations/Scripts/V003__deposits.sql`:
```sql
CREATE TABLE deposits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    circle_payment_intent_id TEXT NOT NULL DEFAULT '',
    amount NUMERIC(18,6) NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- [ ] **Step 4: Create V004__withdrawals.sql**

`src/FundManagement.Infrastructure/Migrations/Scripts/V004__withdrawals.sql`:
```sql
CREATE TABLE withdrawals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES customers(id),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    circle_payout_id TEXT NOT NULL DEFAULT '',
    amount NUMERIC(18,6) NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- [ ] **Step 5: Create V005__ledger_entries.sql**

`src/FundManagement.Infrastructure/Migrations/Scripts/V005__ledger_entries.sql`:
```sql
CREATE TABLE ledger_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    funding_account_id UUID NOT NULL REFERENCES funding_accounts(id),
    entry_type TEXT NOT NULL,
    amount NUMERIC(18,6) NOT NULL,
    reference_id TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

- [ ] **Step 6: Create V006__webhook_events.sql**

`src/FundManagement.Infrastructure/Migrations/Scripts/V006__webhook_events.sql`:
```sql
CREATE TABLE webhook_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    circle_event_id TEXT NOT NULL UNIQUE,
    event_type TEXT NOT NULL,
    payload JSONB NOT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at TIMESTAMPTZ
);
```

- [ ] **Step 7: Commit**

```bash
git add src/FundManagement.Infrastructure/Migrations/Scripts/
git commit -m "feat: add DB migration scripts for all 6 tables"
```

---

## Task 3: Extend ICircleClient + CircleClient

**Files:**
- Modify: `src/FundManagement.Application/Common/ICircleClient.cs`
- Create: `src/FundManagement.Application/Common/CircleDtos.cs`
- Modify: `src/FundManagement.Infrastructure/Circle/CircleClient.cs`

- [ ] **Step 1: Create CircleDtos.cs**

`src/FundManagement.Application/Common/CircleDtos.cs`:
```csharp
namespace FundManagement.Application.Common;

public record CirclePaymentIntentResponse(string Id, string Status, string? DepositAddress, string? Network);
public record CirclePayoutResponse(string Id, string Status);
```

- [ ] **Step 2: Extend ICircleClient.cs**

Replace `src/FundManagement.Application/Common/ICircleClient.cs`:
```csharp
namespace FundManagement.Application.Common;

public interface ICircleClient
{
    Task<bool> PingAsync(CancellationToken ct = default);
    Task<CirclePaymentIntentResponse> CreatePaymentIntentAsync(decimal amount, string currency, string idempotencyKey, CancellationToken ct = default);
    Task<CirclePaymentIntentResponse> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);
    Task<CirclePayoutResponse> CreatePayoutAsync(decimal amount, string currency, string destinationAddress, string idempotencyKey, CancellationToken ct = default);
    Task<CirclePayoutResponse> GetPayoutAsync(string payoutId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Implement new methods in CircleClient.cs**

Replace `src/FundManagement.Infrastructure/Circle/CircleClient.cs`:
```csharp
using System.Text;
using System.Text.Json;
using FundManagement.Application.Common;
using Microsoft.Extensions.Logging;

namespace FundManagement.Infrastructure.Circle;

public class CircleClient : ICircleClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CircleClient> _logger;
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public CircleClient(HttpClient http, ILogger<CircleClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("/ping", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Circle ping failed");
            return false;
        }
    }

    public async Task<CirclePaymentIntentResponse> CreatePaymentIntentAsync(
        decimal amount, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            idempotencyKey,
            amount = new { amount = amount.ToString("F2"), currency },
            settlementCurrency = currency,
            paymentMethods = new[] { new { type = "blockchain", chain = "ETH" } }
        });

        using var response = await _http.PostAsync("/v1/paymentIntents",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePaymentIntentResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!,
            TryGetString(data, "depositAddress", "address"),
            TryGetString(data, "depositAddress", "chain"));
    }

    public async Task<CirclePaymentIntentResponse> GetPaymentIntentAsync(
        string paymentIntentId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"/v1/paymentIntents/{paymentIntentId}", ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePaymentIntentResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!,
            TryGetString(data, "depositAddress", "address"),
            TryGetString(data, "depositAddress", "chain"));
    }

    public async Task<CirclePayoutResponse> CreatePayoutAsync(
        decimal amount, string currency, string destinationAddress, string idempotencyKey, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            idempotencyKey,
            amount = new { amount = amount.ToString("F2"), currency },
            destination = new { type = "blockchain", address = destinationAddress, chain = "ETH" }
        });

        using var response = await _http.PostAsync("/v1/businessAccount/payouts",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePayoutResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!);
    }

    public async Task<CirclePayoutResponse> GetPayoutAsync(string payoutId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"/v1/businessAccount/payouts/{payoutId}", ct);
        response.EnsureSuccessStatusCode();

        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data");

        return new CirclePayoutResponse(
            data.GetProperty("id").GetString()!,
            data.GetProperty("status").GetString()!);
    }

    private static string? TryGetString(JsonElement element, string prop1, string prop2)
    {
        if (element.TryGetProperty(prop1, out var inner) &&
            inner.TryGetProperty(prop2, out var leaf))
            return leaf.GetString();
        return null;
    }
}
```

- [ ] **Step 4: Build check**

```bash
dotnet build src/FundManagement.Infrastructure/FundManagement.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/FundManagement.Application/Common/
git add src/FundManagement.Infrastructure/Circle/CircleClient.cs
git commit -m "feat: extend ICircleClient with payment intent and payout methods"
```

---

## Task 4: DapperConfig + ReconciliationResult

**Files:**
- Create: `src/FundManagement.Infrastructure/Data/DapperConfig.cs`
- Create: `src/FundManagement.Application/Reconciliation/ReconciliationResult.cs`

- [ ] **Step 1: Create DapperConfig.cs**

Dapper maps enum columns stored as TEXT to C# enums via a TypeHandler.

`src/FundManagement.Infrastructure/Data/DapperConfig.cs`:
```csharp
using System.Data;
using Dapper;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Data;

public static class DapperConfig
{
    public static void Configure()
    {
        SqlMapper.AddTypeHandler(new EnumTypeHandler<CustomerType>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<DepositStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<WithdrawalStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<EntryType>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<WebhookStatus>());
    }
}

file class EnumTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override T Parse(object value) => Enum.Parse<T>((string)value);
    public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value.ToString();
}
```

- [ ] **Step 2: Create ReconciliationResult.cs**

`src/FundManagement.Application/Reconciliation/ReconciliationResult.cs`:
```csharp
namespace FundManagement.Application.Reconciliation;

public record ReconciliationResult(
    DateTimeOffset RunAt,
    int TotalDeposits,
    int MatchedDeposits,
    int UnmatchedDeposits,
    int TotalWithdrawals,
    int MatchedWithdrawals,
    int UnmatchedWithdrawals,
    IReadOnlyList<string> Mismatches);
```

- [ ] **Step 3: Commit**

```bash
git add src/FundManagement.Infrastructure/Data/DapperConfig.cs
git add src/FundManagement.Application/Reconciliation/ReconciliationResult.cs
git commit -m "feat: add Dapper enum type handlers and ReconciliationResult record"
```

---

## Task 5: Application Service Interfaces

**Files:**
- Create: `src/FundManagement.Application/Customers/ICustomerService.cs`
- Create: `src/FundManagement.Application/Deposits/IDepositService.cs`
- Create: `src/FundManagement.Application/Withdrawals/IWithdrawalService.cs`
- Create: `src/FundManagement.Application/Ledger/ILedgerService.cs`
- Create: `src/FundManagement.Application/Webhooks/IWebhookService.cs`
- Create: `src/FundManagement.Application/Reconciliation/IReconciliationService.cs`

- [ ] **Step 1: Create ICustomerService.cs**

`src/FundManagement.Application/Customers/ICustomerService.cs`:
```csharp
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Application.Customers;

public interface ICustomerService
{
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Customer> CreateAsync(string name, string email, CustomerType type);
    Task<IEnumerable<FundingAccount>> GetFundingAccountsAsync(Guid customerId);
    Task<FundingAccount> CreateFundingAccountAsync(Guid customerId, string currency);
}
```

- [ ] **Step 2: Create IDepositService.cs**

`src/FundManagement.Application/Deposits/IDepositService.cs`:
```csharp
using FundManagement.Domain.Entities;

namespace FundManagement.Application.Deposits;

public interface IDepositService
{
    Task<IEnumerable<Deposit>> GetAllAsync();
    Task<IEnumerable<Deposit>> GetByCustomerAsync(Guid customerId);
    Task<Deposit?> GetByIdAsync(Guid id);
    Task<Deposit> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount);
    Task ProcessSettlementAsync(string circlePaymentIntentId, string circleStatus);
}
```

- [ ] **Step 3: Create IWithdrawalService.cs**

`src/FundManagement.Application/Withdrawals/IWithdrawalService.cs`:
```csharp
using FundManagement.Domain.Entities;

namespace FundManagement.Application.Withdrawals;

public interface IWithdrawalService
{
    Task<IEnumerable<Withdrawal>> GetAllAsync();
    Task<IEnumerable<Withdrawal>> GetByCustomerAsync(Guid customerId);
    Task<Withdrawal?> GetByIdAsync(Guid id);
    Task<Withdrawal> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount, string destinationAddress);
    Task ProcessPayoutSettlementAsync(string circlePayoutId, string circleStatus);
}
```

- [ ] **Step 4: Create ILedgerService.cs**

`src/FundManagement.Application/Ledger/ILedgerService.cs`:
```csharp
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Application.Ledger;

public interface ILedgerService
{
    Task<IEnumerable<LedgerEntry>> GetByFundingAccountAsync(Guid fundingAccountId);
    Task<decimal> GetBalanceAsync(Guid fundingAccountId);
    Task<LedgerEntry> CreateEntryAsync(Guid fundingAccountId, EntryType entryType, decimal amount, string referenceId);
}
```

- [ ] **Step 5: Create IWebhookService.cs**

`src/FundManagement.Application/Webhooks/IWebhookService.cs`:
```csharp
using FundManagement.Domain.Entities;

namespace FundManagement.Application.Webhooks;

public interface IWebhookService
{
    Task<IEnumerable<WebhookEvent>> GetAllAsync();
    Task ProcessAsync(string circleEventId, string eventType, string payload);
}
```

- [ ] **Step 6: Create IReconciliationService.cs**

`src/FundManagement.Application/Reconciliation/IReconciliationService.cs`:
```csharp
namespace FundManagement.Application.Reconciliation;

public interface IReconciliationService
{
    Task<ReconciliationResult> RunAsync();
}
```

- [ ] **Step 7: Build check**

```bash
dotnet build src/FundManagement.Application/FundManagement.Application.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add src/FundManagement.Application/
git commit -m "feat: add service interfaces for all 6 features"
```

---

## Task 6: CustomerService + LedgerService

**Files:**
- Create: `src/FundManagement.Infrastructure/Services/CustomerService.cs`
- Create: `src/FundManagement.Infrastructure/Services/LedgerService.cs`

- [ ] **Step 1: Create CustomerService.cs**

`src/FundManagement.Infrastructure/Services/CustomerService.cs`:
```csharp
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Customers;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly IDbConnectionFactory _db;

    public CustomerService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Customer>(
            "SELECT * FROM customers ORDER BY created_at DESC");
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Customer>(
            "SELECT * FROM customers WHERE id = @Id", new { Id = id });
    }

    public async Task<Customer> CreateAsync(string name, string email, CustomerType type)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<Customer>(
            @"INSERT INTO customers (id, name, email, customer_type, created_at)
              VALUES (gen_random_uuid(), @Name, @Email, @Type, now())
              RETURNING *",
            new { Name = name, Email = email, Type = type.ToString() });
    }

    public async Task<IEnumerable<FundingAccount>> GetFundingAccountsAsync(Guid customerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FundingAccount>(
            "SELECT * FROM funding_accounts WHERE customer_id = @CustomerId ORDER BY created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<FundingAccount> CreateFundingAccountAsync(Guid customerId, string currency)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<FundingAccount>(
            @"INSERT INTO funding_accounts (id, customer_id, currency, created_at)
              VALUES (gen_random_uuid(), @CustomerId, @Currency, now())
              RETURNING *",
            new { CustomerId = customerId, Currency = currency });
    }
}
```

- [ ] **Step 2: Create LedgerService.cs**

`src/FundManagement.Infrastructure/Services/LedgerService.cs`:
```csharp
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Ledger;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class LedgerService : ILedgerService
{
    private readonly IDbConnectionFactory _db;

    public LedgerService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<LedgerEntry>> GetByFundingAccountAsync(Guid fundingAccountId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<LedgerEntry>(
            "SELECT * FROM ledger_entries WHERE funding_account_id = @FundingAccountId ORDER BY created_at ASC",
            new { FundingAccountId = fundingAccountId });
    }

    public async Task<decimal> GetBalanceAsync(Guid fundingAccountId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<decimal>(
            @"SELECT COALESCE(
                SUM(CASE WHEN entry_type = 'Credit' THEN amount ELSE -amount END),
              0)
              FROM ledger_entries
              WHERE funding_account_id = @FundingAccountId",
            new { FundingAccountId = fundingAccountId });
    }

    public async Task<LedgerEntry> CreateEntryAsync(
        Guid fundingAccountId, EntryType entryType, decimal amount, string referenceId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<LedgerEntry>(
            @"INSERT INTO ledger_entries (id, funding_account_id, entry_type, amount, reference_id, created_at)
              VALUES (gen_random_uuid(), @FundingAccountId, @EntryType, @Amount, @ReferenceId, now())
              RETURNING *",
            new { FundingAccountId = fundingAccountId, EntryType = entryType.ToString(), Amount = amount, ReferenceId = referenceId });
    }
}
```

- [ ] **Step 3: Build check**

```bash
dotnet build src/FundManagement.Infrastructure/FundManagement.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/FundManagement.Infrastructure/Services/CustomerService.cs
git add src/FundManagement.Infrastructure/Services/LedgerService.cs
git commit -m "feat: implement CustomerService and LedgerService"
```

---

## Task 7: DepositService + WithdrawalService

**Files:**
- Create: `src/FundManagement.Infrastructure/Services/DepositService.cs`
- Create: `src/FundManagement.Infrastructure/Services/WithdrawalService.cs`

- [ ] **Step 1: Create DepositService.cs**

`src/FundManagement.Infrastructure/Services/DepositService.cs`:
```csharp
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Deposits;
using FundManagement.Application.Ledger;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class DepositService : IDepositService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICircleClient _circle;
    private readonly ILedgerService _ledger;

    public DepositService(IDbConnectionFactory db, ICircleClient circle, ILedgerService ledger)
    {
        _db = db;
        _circle = circle;
        _ledger = ledger;
    }

    public async Task<IEnumerable<Deposit>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Deposit>(
            "SELECT * FROM deposits ORDER BY created_at DESC");
    }

    public async Task<IEnumerable<Deposit>> GetByCustomerAsync(Guid customerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Deposit>(
            "SELECT * FROM deposits WHERE customer_id = @CustomerId ORDER BY created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<Deposit?> GetByIdAsync(Guid id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Deposit>(
            "SELECT * FROM deposits WHERE id = @Id", new { Id = id });
    }

    public async Task<Deposit> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount)
    {
        var intent = await _circle.CreatePaymentIntentAsync(
            amount, "USD", Guid.NewGuid().ToString());

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<Deposit>(
            @"INSERT INTO deposits
                (id, customer_id, funding_account_id, circle_payment_intent_id, amount, status, created_at, updated_at)
              VALUES
                (gen_random_uuid(), @CustomerId, @FundingAccountId, @CirclePaymentIntentId, @Amount, @Status, now(), now())
              RETURNING *",
            new
            {
                CustomerId = customerId,
                FundingAccountId = fundingAccountId,
                CirclePaymentIntentId = intent.Id,
                Amount = amount,
                Status = DepositStatus.Pending.ToString()
            });
    }

    public async Task ProcessSettlementAsync(string circlePaymentIntentId, string circleStatus)
    {
        using var conn = _db.CreateConnection();
        var deposit = await conn.QuerySingleOrDefaultAsync<Deposit>(
            "SELECT * FROM deposits WHERE circle_payment_intent_id = @Id",
            new { Id = circlePaymentIntentId });

        if (deposit == null) return;

        var newStatus = circleStatus == "complete" ? DepositStatus.Completed : DepositStatus.Failed;

        await conn.ExecuteAsync(
            "UPDATE deposits SET status = @Status, updated_at = now() WHERE id = @Id",
            new { Status = newStatus.ToString(), Id = deposit.Id });

        if (newStatus == DepositStatus.Completed)
            await _ledger.CreateEntryAsync(
                deposit.FundingAccountId, EntryType.Credit, deposit.Amount, circlePaymentIntentId);
    }
}
```

- [ ] **Step 2: Create WithdrawalService.cs**

`src/FundManagement.Infrastructure/Services/WithdrawalService.cs`:
```csharp
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Ledger;
using FundManagement.Application.Withdrawals;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICircleClient _circle;
    private readonly ILedgerService _ledger;

    public WithdrawalService(IDbConnectionFactory db, ICircleClient circle, ILedgerService ledger)
    {
        _db = db;
        _circle = circle;
        _ledger = ledger;
    }

    public async Task<IEnumerable<Withdrawal>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Withdrawal>(
            "SELECT * FROM withdrawals ORDER BY created_at DESC");
    }

    public async Task<IEnumerable<Withdrawal>> GetByCustomerAsync(Guid customerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Withdrawal>(
            "SELECT * FROM withdrawals WHERE customer_id = @CustomerId ORDER BY created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<Withdrawal?> GetByIdAsync(Guid id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Withdrawal>(
            "SELECT * FROM withdrawals WHERE id = @Id", new { Id = id });
    }

    public async Task<Withdrawal> CreateAsync(
        Guid customerId, Guid fundingAccountId, decimal amount, string destinationAddress)
    {
        var balance = await _ledger.GetBalanceAsync(fundingAccountId);
        if (balance < amount)
            throw new InvalidOperationException($"Insufficient balance: {balance} < {amount}");

        var payout = await _circle.CreatePayoutAsync(
            amount, "USD", destinationAddress, Guid.NewGuid().ToString());

        using var conn = _db.CreateConnection();
        var withdrawal = await conn.QuerySingleAsync<Withdrawal>(
            @"INSERT INTO withdrawals
                (id, customer_id, funding_account_id, circle_payout_id, amount, status, created_at, updated_at)
              VALUES
                (gen_random_uuid(), @CustomerId, @FundingAccountId, @CirclePayoutId, @Amount, @Status, now(), now())
              RETURNING *",
            new
            {
                CustomerId = customerId,
                FundingAccountId = fundingAccountId,
                CirclePayoutId = payout.Id,
                Amount = amount,
                Status = WithdrawalStatus.Pending.ToString()
            });

        await _ledger.CreateEntryAsync(
            fundingAccountId, EntryType.Debit, amount, payout.Id);

        return withdrawal;
    }

    public async Task ProcessPayoutSettlementAsync(string circlePayoutId, string circleStatus)
    {
        using var conn = _db.CreateConnection();
        var newStatus = circleStatus == "complete"
            ? WithdrawalStatus.Completed
            : WithdrawalStatus.Failed;

        await conn.ExecuteAsync(
            "UPDATE withdrawals SET status = @Status, updated_at = now() WHERE circle_payout_id = @PayoutId",
            new { Status = newStatus.ToString(), PayoutId = circlePayoutId });

        if (newStatus == WithdrawalStatus.Failed)
        {
            var withdrawal = await conn.QuerySingleOrDefaultAsync<Withdrawal>(
                "SELECT * FROM withdrawals WHERE circle_payout_id = @PayoutId",
                new { PayoutId = circlePayoutId });

            if (withdrawal != null)
                await _ledger.CreateEntryAsync(
                    withdrawal.FundingAccountId, EntryType.Credit,
                    withdrawal.Amount, $"reversal:{circlePayoutId}");
        }
    }
}
```

- [ ] **Step 3: Build check**

```bash
dotnet build src/FundManagement.Infrastructure/FundManagement.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/FundManagement.Infrastructure/Services/DepositService.cs
git add src/FundManagement.Infrastructure/Services/WithdrawalService.cs
git commit -m "feat: implement DepositService and WithdrawalService"
```

---

## Task 8: WebhookService

**Files:**
- Create: `src/FundManagement.Infrastructure/Services/WebhookService.cs`

- [ ] **Step 1: Create WebhookService.cs**

`src/FundManagement.Infrastructure/Services/WebhookService.cs`:
```csharp
using System.Text.Json;
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Deposits;
using FundManagement.Application.Webhooks;
using FundManagement.Application.Withdrawals;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly IDbConnectionFactory _db;
    private readonly IDepositService _deposits;
    private readonly IWithdrawalService _withdrawals;

    public WebhookService(IDbConnectionFactory db, IDepositService deposits, IWithdrawalService withdrawals)
    {
        _db = db;
        _deposits = deposits;
        _withdrawals = withdrawals;
    }

    public async Task<IEnumerable<WebhookEvent>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<WebhookEvent>(
            "SELECT * FROM webhook_events ORDER BY created_at DESC");
    }

    public async Task ProcessAsync(string circleEventId, string eventType, string payload)
    {
        using var conn = _db.CreateConnection();

        var affected = await conn.ExecuteAsync(
            @"INSERT INTO webhook_events
                (id, circle_event_id, event_type, payload, status, created_at)
              VALUES
                (gen_random_uuid(), @EventId, @EventType, @Payload::jsonb, @Status, now())
              ON CONFLICT (circle_event_id) DO NOTHING",
            new { EventId = circleEventId, EventType = eventType, Payload = payload, Status = WebhookStatus.Received.ToString() });

        if (affected == 0) return; // duplicate

        try
        {
            await DispatchAsync(eventType, payload);

            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status, processed_at = now() WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Processed.ToString(), EventId = circleEventId });
        }
        catch
        {
            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Failed.ToString(), EventId = circleEventId });
            throw;
        }
    }

    private async Task DispatchAsync(string eventType, string payload)
    {
        var doc = JsonDocument.Parse(payload);
        switch (eventType)
        {
            case "payments.payment_intent.completed":
                await _deposits.ProcessSettlementAsync(
                    doc.RootElement.GetProperty("paymentIntentId").GetString()!, "complete");
                break;
            case "payments.payment_intent.failed":
                await _deposits.ProcessSettlementAsync(
                    doc.RootElement.GetProperty("paymentIntentId").GetString()!, "failed");
                break;
            case "payouts.payout.complete":
                await _withdrawals.ProcessPayoutSettlementAsync(
                    doc.RootElement.GetProperty("payoutId").GetString()!, "complete");
                break;
            case "payouts.payout.failed":
                await _withdrawals.ProcessPayoutSettlementAsync(
                    doc.RootElement.GetProperty("payoutId").GetString()!, "failed");
                break;
        }
    }
}
```

- [ ] **Step 2: Build check**

```bash
dotnet build src/FundManagement.Infrastructure/FundManagement.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/FundManagement.Infrastructure/Services/WebhookService.cs
git commit -m "feat: implement WebhookService with idempotency guard"
```

---

## Task 9: ReconciliationService

**Files:**
- Create: `src/FundManagement.Infrastructure/Services/ReconciliationService.cs`

- [ ] **Step 1: Create ReconciliationService.cs**

`src/FundManagement.Infrastructure/Services/ReconciliationService.cs`:
```csharp
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Reconciliation;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICircleClient _circle;

    public ReconciliationService(IDbConnectionFactory db, ICircleClient circle)
    {
        _db = db;
        _circle = circle;
    }

    public async Task<ReconciliationResult> RunAsync()
    {
        using var conn = _db.CreateConnection();
        var deposits = (await conn.QueryAsync<Deposit>("SELECT * FROM deposits")).ToList();
        var withdrawals = (await conn.QueryAsync<Withdrawal>("SELECT * FROM withdrawals")).ToList();

        var mismatches = new List<string>();
        int matchedDeposits = 0, unmatchedDeposits = 0;

        foreach (var deposit in deposits.Where(d => d.CirclePaymentIntentId != string.Empty))
        {
            var circle = await _circle.GetPaymentIntentAsync(deposit.CirclePaymentIntentId);
            var expected = circle.Status == "complete" ? DepositStatus.Completed
                         : circle.Status == "failed" ? DepositStatus.Failed
                         : DepositStatus.Pending;

            if (deposit.Status == expected)
                matchedDeposits++;
            else
            {
                unmatchedDeposits++;
                mismatches.Add($"Deposit {deposit.Id}: local={deposit.Status} circle={circle.Status}");
            }
        }

        int matchedWithdrawals = 0, unmatchedWithdrawals = 0;

        foreach (var withdrawal in withdrawals.Where(w => w.CirclePayoutId != string.Empty))
        {
            var circle = await _circle.GetPayoutAsync(withdrawal.CirclePayoutId);
            var expected = circle.Status == "complete" ? WithdrawalStatus.Completed
                         : circle.Status == "failed" ? WithdrawalStatus.Failed
                         : WithdrawalStatus.Pending;

            if (withdrawal.Status == expected)
                matchedWithdrawals++;
            else
            {
                unmatchedWithdrawals++;
                mismatches.Add($"Withdrawal {withdrawal.Id}: local={withdrawal.Status} circle={circle.Status}");
            }
        }

        return new ReconciliationResult(
            DateTimeOffset.UtcNow,
            deposits.Count,
            matchedDeposits,
            unmatchedDeposits,
            withdrawals.Count,
            matchedWithdrawals,
            unmatchedWithdrawals,
            mismatches);
    }
}
```

- [ ] **Step 2: Build check**

```bash
dotnet build src/FundManagement.Infrastructure/FundManagement.Infrastructure.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/FundManagement.Infrastructure/Services/ReconciliationService.cs
git commit -m "feat: implement ReconciliationService"
```

---

## Task 10: DI Wiring in Program.cs

> Endpoint mapping calls (`MapXEndpoints`) are added in Task 13 after all endpoint files exist. Adding them here would break the build.

**Files:**
- Modify: `src/FundManagement.Api/Program.cs`

- [ ] **Step 1: Replace Program.cs with DI-wired version (no endpoint mapping yet)**

`src/FundManagement.Api/Program.cs`:
```csharp
using System.Net.Http.Headers;
using FundManagement.Api.Endpoints;
using FundManagement.Application.Common;
using FundManagement.Application.Customers;
using FundManagement.Application.Deposits;
using FundManagement.Application.Ledger;
using FundManagement.Application.Reconciliation;
using FundManagement.Application.Webhooks;
using FundManagement.Application.Withdrawals;
using FundManagement.Infrastructure.Circle;
using FundManagement.Infrastructure.Data;
using FundManagement.Infrastructure.Migrations;
using FundManagement.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new DbConnectionFactory(connectionString));

DapperConfig.Configure();

builder.Services.AddHttpClient<ICircleClient, CircleClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Circle:BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Circle:ApiKey"]);
});

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IDepositService, DepositService>();
builder.Services.AddScoped<IWithdrawalService, WithdrawalService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

new MigrationRunner(connectionString).Run();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();

app.MapHealthEndpoints();

app.Run();
```

- [ ] **Step 2: Build check**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/FundManagement.Api/Program.cs
git commit -m "feat: wire up all services in Program.cs"
```

---

## Task 11: CustomerEndpoints + DepositEndpoints

**Files:**
- Create: `src/FundManagement.Api/Endpoints/CustomerEndpoints.cs`
- Create: `src/FundManagement.Api/Endpoints/DepositEndpoints.cs`

- [ ] **Step 1: Create CustomerEndpoints.cs**

`src/FundManagement.Api/Endpoints/CustomerEndpoints.cs`:
```csharp
using FundManagement.Application.Customers;
using FundManagement.Domain.Enums;

namespace FundManagement.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/customers").WithTags("Customers");

        g.MapGet("/", async (ICustomerService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapGet("/{id:guid}", async (Guid id, ICustomerService svc) =>
        {
            var c = await svc.GetByIdAsync(id);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });

        g.MapPost("/", async (CreateCustomerRequest req, ICustomerService svc) =>
        {
            var c = await svc.CreateAsync(req.Name, req.Email, req.CustomerType);
            return Results.Created($"/customers/{c.Id}", c);
        });

        g.MapGet("/{id:guid}/funding-accounts", async (Guid id, ICustomerService svc) =>
            Results.Ok(await svc.GetFundingAccountsAsync(id)));

        g.MapPost("/{id:guid}/funding-accounts", async (Guid id, CreateFundingAccountRequest req, ICustomerService svc) =>
        {
            var fa = await svc.CreateFundingAccountAsync(id, req.Currency);
            return Results.Created($"/customers/{id}/funding-accounts/{fa.Id}", fa);
        });
    }
}

public record CreateCustomerRequest(string Name, string Email, CustomerType CustomerType);
public record CreateFundingAccountRequest(string Currency);
```

- [ ] **Step 2: Create DepositEndpoints.cs**

`src/FundManagement.Api/Endpoints/DepositEndpoints.cs`:
```csharp
using FundManagement.Application.Deposits;

namespace FundManagement.Api.Endpoints;

public static class DepositEndpoints
{
    public static void MapDepositEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/deposits").WithTags("Deposits");

        g.MapGet("/", async (IDepositService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapGet("/{id:guid}", async (Guid id, IDepositService svc) =>
        {
            var d = await svc.GetByIdAsync(id);
            return d is null ? Results.NotFound() : Results.Ok(d);
        });

        g.MapPost("/", async (CreateDepositRequest req, IDepositService svc) =>
        {
            var d = await svc.CreateAsync(req.CustomerId, req.FundingAccountId, req.Amount);
            return Results.Created($"/deposits/{d.Id}", d);
        });
    }
}

public record CreateDepositRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount);
```

- [ ] **Step 3: Commit**

```bash
git add src/FundManagement.Api/Endpoints/CustomerEndpoints.cs
git add src/FundManagement.Api/Endpoints/DepositEndpoints.cs
git commit -m "feat: add CustomerEndpoints and DepositEndpoints"
```

---

## Task 12: WithdrawalEndpoints + LedgerEndpoints

**Files:**
- Create: `src/FundManagement.Api/Endpoints/WithdrawalEndpoints.cs`
- Create: `src/FundManagement.Api/Endpoints/LedgerEndpoints.cs`

- [ ] **Step 1: Create WithdrawalEndpoints.cs**

`src/FundManagement.Api/Endpoints/WithdrawalEndpoints.cs`:
```csharp
using FundManagement.Application.Withdrawals;

namespace FundManagement.Api.Endpoints;

public static class WithdrawalEndpoints
{
    public static void MapWithdrawalEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/withdrawals").WithTags("Withdrawals");

        g.MapGet("/", async (IWithdrawalService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapGet("/{id:guid}", async (Guid id, IWithdrawalService svc) =>
        {
            var w = await svc.GetByIdAsync(id);
            return w is null ? Results.NotFound() : Results.Ok(w);
        });

        g.MapPost("/", async (CreateWithdrawalRequest req, IWithdrawalService svc) =>
        {
            try
            {
                var w = await svc.CreateAsync(
                    req.CustomerId, req.FundingAccountId, req.Amount, req.DestinationAddress);
                return Results.Created($"/withdrawals/{w.Id}", w);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}

public record CreateWithdrawalRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount, string DestinationAddress);
```

- [ ] **Step 2: Create LedgerEndpoints.cs**

`src/FundManagement.Api/Endpoints/LedgerEndpoints.cs`:
```csharp
using FundManagement.Application.Ledger;

namespace FundManagement.Api.Endpoints;

public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/funding-accounts").WithTags("Ledger");

        g.MapGet("/{id:guid}/ledger", async (Guid id, ILedgerService svc) =>
            Results.Ok(await svc.GetByFundingAccountAsync(id)));

        g.MapGet("/{id:guid}/balance", async (Guid id, ILedgerService svc) =>
            Results.Ok(new { balance = await svc.GetBalanceAsync(id) }));
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/FundManagement.Api/Endpoints/WithdrawalEndpoints.cs
git add src/FundManagement.Api/Endpoints/LedgerEndpoints.cs
git commit -m "feat: add WithdrawalEndpoints and LedgerEndpoints"
```

---

## Task 13: WebhookEndpoints + ReconciliationEndpoints

**Files:**
- Create: `src/FundManagement.Api/Endpoints/WebhookEndpoints.cs`
- Create: `src/FundManagement.Api/Endpoints/ReconciliationEndpoints.cs`

- [ ] **Step 1: Create WebhookEndpoints.cs**

`src/FundManagement.Api/Endpoints/WebhookEndpoints.cs`:
```csharp
using FundManagement.Application.Webhooks;

namespace FundManagement.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/webhooks").WithTags("Webhooks");

        g.MapGet("/", async (IWebhookService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapPost("/circle", async (CircleWebhookRequest req, IWebhookService svc) =>
        {
            await svc.ProcessAsync(req.EventId, req.EventType, req.Payload);
            return Results.Ok();
        });
    }
}

public record CircleWebhookRequest(string EventId, string EventType, string Payload);
```

- [ ] **Step 2: Create ReconciliationEndpoints.cs**

`src/FundManagement.Api/Endpoints/ReconciliationEndpoints.cs`:
```csharp
using FundManagement.Application.Reconciliation;

namespace FundManagement.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static void MapReconciliationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/reconciliation").WithTags("Reconciliation");

        g.MapPost("/run", async (IReconciliationService svc) =>
            Results.Ok(await svc.RunAsync()));
    }
}
```

- [ ] **Step 3: Wire all endpoint groups into Program.cs**

All endpoint files now exist. Add the `MapXEndpoints` calls to `src/FundManagement.Api/Program.cs` — replace the `app.MapHealthEndpoints();` + `app.Run();` block at the end with:

```csharp
app.MapHealthEndpoints();
app.MapCustomerEndpoints();
app.MapDepositEndpoints();
app.MapWithdrawalEndpoints();
app.MapLedgerEndpoints();
app.MapWebhookEndpoints();
app.MapReconciliationEndpoints();

app.Run();
```

- [ ] **Step 4: Full build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/FundManagement.Api/Endpoints/WebhookEndpoints.cs
git add src/FundManagement.Api/Endpoints/ReconciliationEndpoints.cs
git add src/FundManagement.Api/Program.cs
git commit -m "feat: add WebhookEndpoints, ReconciliationEndpoints, wire all routes"
```

---

## Task 14: Smoke Test

> Requires: PostgreSQL running locally (`docker run -d --name ifs-pg -e POSTGRES_PASSWORD=localdev -e POSTGRES_DB=ifs_poc -p 5432:5432 postgres:16`)
> Requires: user secrets set (`dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ifs_poc;Username=postgres;Password=localdev"` and `dotnet user-secrets set "Circle:BaseUrl" "https://api-sandbox.circle.com"` and `dotnet user-secrets set "Circle:ApiKey" "SAND_..."`)

- [ ] **Step 1: Start the API**

```bash
dotnet run --project src/FundManagement.Api
```

Expected console output contains: migration scripts executed (or "No new scripts"), then `Now listening on: http://localhost:5000`

- [ ] **Step 2: Verify health endpoint**

```bash
curl http://localhost:5000/health
```

Expected: `200 OK`

- [ ] **Step 3: Create a customer**

```bash
curl -s -X POST http://localhost:5000/customers \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com","customerType":"ExternalWallet"}' | jq .
```

Expected: `201 Created` with customer JSON including `id`.

- [ ] **Step 4: Create a funding account for that customer**

```bash
curl -s -X POST http://localhost:5000/customers/{id}/funding-accounts \
  -H "Content-Type: application/json" \
  -d '{"currency":"USDC"}' | jq .
```

Replace `{id}` with the customer id from Step 3. Expected: `201 Created` with funding account JSON.

- [ ] **Step 5: Check Swagger**

Open `http://localhost:5000/swagger` — all 6 tag groups (Customers, Deposits, Withdrawals, Ledger, Webhooks, Reconciliation) should be visible.

- [ ] **Step 6: Final commit**

```bash
git add .
git commit -m "feat: complete full vertical slice — services, migrations, endpoints, no MediatR"
```
