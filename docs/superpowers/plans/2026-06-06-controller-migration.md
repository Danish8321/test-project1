# Controller Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all 7 minimal API endpoint files with standard `[ApiController]` controllers, preserving every route, status code, and business logic call exactly.

**Architecture:** Create `Controllers/` and `Models/Requests/` folders inside `FundManagement.Api`. Tasks 1–8 add new files only (safe to build at each step). Task 9 atomically swaps `Program.cs` and deletes the old `Endpoints/` folder.

**Tech Stack:** .NET 10, ASP.NET Core MVC (`Microsoft.NET.Sdk.Web` already includes controller support — no new packages).

---

## File Map

```
api/src/FundManagement.Api/
  Models/
    Requests/
      CustomerRequests.cs        Task 1 — CREATE
      DepositRequests.cs         Task 1 — CREATE
      WithdrawalRequests.cs      Task 1 — CREATE
  Controllers/
    HealthController.cs          Task 2 — CREATE
    CustomersController.cs       Task 3 — CREATE
    DepositsController.cs        Task 4 — CREATE
    WithdrawalsController.cs     Task 5 — CREATE
    FundingAccountsController.cs Task 6 — CREATE
    WebhooksController.cs        Task 7 — CREATE
    ReconciliationController.cs  Task 8 — CREATE
  Program.cs                     Task 9 — MODIFY
  Endpoints/                     Task 9 — DELETE (all 7 files)
```

---

### Task 1: Request Models

**Files:**
- Create: `api/src/FundManagement.Api/Models/Requests/CustomerRequests.cs`
- Create: `api/src/FundManagement.Api/Models/Requests/DepositRequests.cs`
- Create: `api/src/FundManagement.Api/Models/Requests/WithdrawalRequests.cs`

These records move out of the old endpoint files. Types and property names are unchanged.

- [ ] **Step 1: Create `CustomerRequests.cs`**

```csharp
using FundManagement.Domain.Enums;

namespace FundManagement.Api.Models.Requests;

public record CreateCustomerRequest(string Name, string Email, CustomerType CustomerType);
public record CreateFundingAccountRequest(string Currency);
```

- [ ] **Step 2: Create `DepositRequests.cs`**

```csharp
namespace FundManagement.Api.Models.Requests;

public record CreateDepositRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount);
```

- [ ] **Step 3: Create `WithdrawalRequests.cs`**

```csharp
namespace FundManagement.Api.Models.Requests;

public record CreateWithdrawalRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount, string DestinationAddress);
```

- [ ] **Step 4: Verify build**

Run from `api/`:
```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.` (old endpoint files still present — no conflicts yet)

- [ ] **Step 5: Commit**

```bash
git add api/src/FundManagement.Api/Models/
git commit -m "feat: add controller request models to Models/Requests/"
```

---

### Task 2: HealthController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/HealthController.cs`

Route: `GET /health` — same logic as `HealthEndpoint.cs`, services injected via primary constructor.

- [ ] **Step 1: Create `HealthController.cs`**

```csharp
using Dapper;
using FundManagement.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(IDbConnectionFactory dbFactory, ICircleClient circle) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var dbStatus = "error";
        var circleStatus = "error";

        try
        {
            using var db = dbFactory.CreateConnection();
            await db.ExecuteScalarAsync("SELECT 1");
            dbStatus = "ok";
        }
        catch { }

        try
        {
            circleStatus = await circle.PingAsync() ? "ok" : "error";
        }
        catch { }

        return Ok(new
        {
            db = dbStatus,
            circle = circleStatus,
            timestamp = DateTime.UtcNow
        });
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/HealthController.cs
git commit -m "feat: add HealthController"
```

---

### Task 3: CustomersController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/CustomersController.cs`

Routes: `GET /customers`, `GET /customers/{id}`, `POST /customers`, `GET /customers/{id}/funding-accounts`, `POST /customers/{id}/funding-accounts`.

- [ ] **Step 1: Create `CustomersController.cs`**

```csharp
using FundManagement.Api.Models.Requests;
using FundManagement.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("customers")]
public class CustomersController(ICustomerService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await svc.GetByIdAsync(id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest req)
    {
        var c = await svc.CreateAsync(req.Name, req.Email, req.CustomerType);
        return Created($"/customers/{c.Id}", c);
    }

    [HttpGet("{id:guid}/funding-accounts")]
    public async Task<IActionResult> GetFundingAccounts(Guid id) =>
        Ok(await svc.GetFundingAccountsAsync(id));

    [HttpPost("{id:guid}/funding-accounts")]
    public async Task<IActionResult> CreateFundingAccount(Guid id, CreateFundingAccountRequest req)
    {
        var fa = await svc.CreateFundingAccountAsync(id, req.Currency);
        return Created($"/customers/{id}/funding-accounts/{fa.Id}", fa);
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/CustomersController.cs
git commit -m "feat: add CustomersController"
```

---

### Task 4: DepositsController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/DepositsController.cs`

Routes: `GET /deposits`, `GET /deposits/{id}`, `POST /deposits`. Preserves `HttpRequestException` catch → `BadRequest`.

- [ ] **Step 1: Create `DepositsController.cs`**

```csharp
using FundManagement.Api.Models.Requests;
using FundManagement.Application.Deposits;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("deposits")]
public class DepositsController(IDepositService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var d = await svc.GetByIdAsync(id);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepositRequest req)
    {
        try
        {
            var d = await svc.CreateAsync(req.CustomerId, req.FundingAccountId, req.Amount);
            return Created($"/deposits/{d.Id}", d);
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { error = $"Circle API error: {ex.Message}" });
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/DepositsController.cs
git commit -m "feat: add DepositsController"
```

---

### Task 5: WithdrawalsController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/WithdrawalsController.cs`

Routes: `GET /withdrawals`, `GET /withdrawals/{id}`, `POST /withdrawals`. Preserves `InvalidOperationException` catch → `BadRequest`.

- [ ] **Step 1: Create `WithdrawalsController.cs`**

```csharp
using FundManagement.Api.Models.Requests;
using FundManagement.Application.Withdrawals;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("withdrawals")]
public class WithdrawalsController(IWithdrawalService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var w = await svc.GetByIdAsync(id);
        return w is null ? NotFound() : Ok(w);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWithdrawalRequest req)
    {
        try
        {
            var w = await svc.CreateAsync(
                req.CustomerId, req.FundingAccountId, req.Amount, req.DestinationAddress);
            return Created($"/withdrawals/{w.Id}", w);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/WithdrawalsController.cs
git commit -m "feat: add WithdrawalsController"
```

---

### Task 6: FundingAccountsController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/FundingAccountsController.cs`

Routes: `GET /funding-accounts/{id}/ledger`, `GET /funding-accounts/{id}/balance`. No request models needed.

- [ ] **Step 1: Create `FundingAccountsController.cs`**

```csharp
using FundManagement.Application.Ledger;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("funding-accounts")]
public class FundingAccountsController(ILedgerService svc) : ControllerBase
{
    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id) =>
        Ok(await svc.GetByFundingAccountAsync(id));

    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid id) =>
        Ok(new { balance = await svc.GetBalanceAsync(id) });
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/FundingAccountsController.cs
git commit -m "feat: add FundingAccountsController"
```

---

### Task 7: WebhooksController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/WebhooksController.cs`

Routes: `GET /webhooks`, `POST /webhooks/circle`. Raw body read uses `Request.EnableBuffering()` (available on `ControllerBase.Request`) instead of `HttpContext ctx` parameter — identical logic.

- [ ] **Step 1: Create `WebhooksController.cs`**

```csharp
using System.Text;
using FundManagement.Application.Webhooks;
using FundManagement.Infrastructure.Circle;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(IWebhookService svc, CircleSignatureValidator validator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpPost("circle")]
    public async Task<IActionResult> ReceiveCircle()
    {
        // Must read raw body — never parse+re-serialize before verifying (whitespace breaks ECDSA sig)
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Circle-Signature"].FirstOrDefault();
        var keyId = Request.Headers["X-Circle-Key-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(keyId))
            return Unauthorized();

        if (!await validator.VerifyAsync(keyId, signature, rawBody))
            return Unauthorized();

        using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var notificationId = root.GetProperty("notificationId").GetString()!;
        var notificationType = root.GetProperty("notificationType").GetString()!;

        await svc.ProcessAsync(notificationId, notificationType, rawBody);
        return Ok();
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/WebhooksController.cs
git commit -m "feat: add WebhooksController"
```

---

### Task 8: ReconciliationController

**Files:**
- Create: `api/src/FundManagement.Api/Controllers/ReconciliationController.cs`

Route: `POST /reconciliation/run`. No request model needed.

- [ ] **Step 1: Create `ReconciliationController.cs`**

```csharp
using FundManagement.Application.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("reconciliation")]
public class ReconciliationController(IReconciliationService svc) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run() =>
        Ok(await svc.RunAsync());
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add api/src/FundManagement.Api/Controllers/ReconciliationController.cs
git commit -m "feat: add ReconciliationController"
```

---

### Task 9: Swap Program.cs + Delete Endpoints/

**Files:**
- Modify: `api/src/FundManagement.Api/Program.cs`
- Delete: `api/src/FundManagement.Api/Endpoints/` (all 7 files)

Changes to `Program.cs`:
- Remove `using FundManagement.Api.Endpoints;`
- Replace `builder.Services.AddEndpointsApiExplorer();` with `builder.Services.AddControllers();`
- Replace all 7 `app.Map*Endpoints()` calls with `app.MapControllers();`

- [ ] **Step 1: Overwrite `Program.cs` with the following**

```csharp
using System.Net.Http.Headers;
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

builder.Services.AddHttpClient<CircleSignatureValidator>(client =>
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

builder.Services.AddControllers();
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
app.MapControllers();

app.Run();
```

- [ ] **Step 2: Delete all files in `Endpoints/`**

```bash
rm api/src/FundManagement.Api/Endpoints/CustomerEndpoints.cs
rm api/src/FundManagement.Api/Endpoints/DepositEndpoints.cs
rm api/src/FundManagement.Api/Endpoints/WithdrawalEndpoints.cs
rm api/src/FundManagement.Api/Endpoints/LedgerEndpoints.cs
rm api/src/FundManagement.Api/Endpoints/WebhookEndpoints.cs
rm api/src/FundManagement.Api/Endpoints/HealthEndpoint.cs
rm api/src/FundManagement.Api/Endpoints/ReconciliationEndpoints.cs
rmdir api/src/FundManagement.Api/Endpoints/
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/FundManagement.Api/FundManagement.Api.csproj
```
Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add api/src/FundManagement.Api/Program.cs
git add -A api/src/FundManagement.Api/Endpoints/
git commit -m "refactor: replace minimal API endpoints with MVC controllers"
```
