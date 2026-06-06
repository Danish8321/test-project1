# Observability Design — FundManagement.Api

**Date:** 2026-06-06  
**Status:** Approved  
**Approach:** Option A — Serilog (logs) + OpenTelemetry (traces + metrics) + App Insights SDK (Azure)

---

## Overview

Three independent observability pillars, each independently feature-flagged. Single entry point in `Program.cs` via `builder.AddObservability()`. All pillar logic encapsulated in `ObservabilityExtensions.cs`.

---

## Architecture

```
HTTP Request
    │
    ├─► Serilog ──────────────────► Console (CompactJsonFormatter)
    │     ILogger<T> → Serilog       [File / cloud sinks swap-in later]
    │
    ├─► OpenTelemetry SDK ─────────► OTLP exporter (gRPC)
    │     Traces: ASP.NET Core,        Endpoint: placeholder, env-var override
    │             HttpClient, Npgsql
    │     Metrics: ASP.NET Core,
    │              HttpClient, Runtime,
    │              Process
    │     Logs: OTel log bridge
    │
    └─► App Insights SDK ──────────► Azure Application Insights
          Auto-instruments:            ConnectionString: placeholder
          requests, dependencies,
          exceptions, Live Metrics
```

---

## Feature Flags

All pillars toggled via `Observability` config section. No code changes to enable/disable.

### `appsettings.json`

```json
"Observability": {
  "Serilog": {
    "Enabled": true
  },
  "ApplicationInsights": {
    "Enabled": false,
    "ConnectionString": "InstrumentationKey=00000000-0000-0000-0000-000000000000"
  },
  "OpenTelemetry": {
    "Enabled": true,
    "Endpoint": "http://localhost:4317",
    "Protocol": "Grpc"
  }
}
```

Per-environment overrides go in `appsettings.Development.json` (gitignored). Production values via environment variables or Azure App Config.

---

## Strongly-Typed Options

```csharp
// ObservabilityOptions.cs
public class ObservabilityOptions
{
    public SerilogOptions Serilog { get; init; } = new();
    public OpenTelemetryOptions OpenTelemetry { get; init; } = new();
    public ApplicationInsightsOptions ApplicationInsights { get; init; } = new();
}

public class SerilogOptions          { public bool Enabled { get; init; } }
public class ApplicationInsightsOptions
{
    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
}
public class OpenTelemetryOptions
{
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = "http://localhost:4317";
    public string Protocol { get; init; } = "Grpc";
}
```

---

## Extension Method Structure

### `Program.cs` — single call

```csharp
builder.AddObservability();
// ... rest of registrations
var app = builder.Build();
app.UseObservability(); // UseSerilogRequestLogging if Serilog enabled
```

### `ObservabilityExtensions.cs` (in FundManagement.Api)

```csharp
public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var opts = builder.Configuration
            .GetSection("Observability")
            .Get<ObservabilityOptions>() ?? new();

        if (opts.Serilog.Enabled)              builder.ConfigureSerilog();
        if (opts.OpenTelemetry.Enabled)        builder.ConfigureOpenTelemetry(opts.OpenTelemetry);
        if (opts.ApplicationInsights.Enabled)  builder.ConfigureAppInsights(opts.ApplicationInsights);

        return builder;
    }

    public static WebApplication UseObservability(this WebApplication app)
    {
        var opts = app.Configuration
            .GetSection("Observability")
            .Get<ObservabilityOptions>() ?? new();

        if (opts.Serilog.Enabled)
            app.UseSerilogRequestLogging();

        return app;
    }
}
```

Each pillar is a private static method in the same file.

---

## Serilog Configuration

### Packages

| Package | Purpose |
|---------|---------|
| `Serilog.AspNetCore` | `UseSerilog()`, request logging middleware |
| `Serilog.Sinks.Console` | Console output |
| `Serilog.Formatting.Compact` | `CompactJsonFormatter` — structured JSON |
| `Serilog.Enrichers.Environment` | `WithMachineName()` |
| `Serilog.Enrichers.Process` | `WithProcessId()` |
| `Serilog.Enrichers.Thread` | `WithThreadId()` |
| `Serilog.Expressions` | Output template expressions |

### Bootstrap pattern

```csharp
// Before builder — catches startup failures
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try { /* host setup */ }
catch (Exception ex) { Log.Fatal(ex, "Host terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }
```

### Main logger (config-driven)

```csharp
builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId());
```

### `Serilog` section in `appsettings.json`

```json
"Serilog": {
  "Using": ["Serilog.Sinks.Console"],
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "System": "Warning"
    }
  },
  "WriteTo": [
    {
      "Name": "Console",
      "Args": {
        "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
      }
    }
  ],
  "Enrich": ["FromLogContext", "WithMachineName", "WithProcessId", "WithThreadId"],
  "Properties": {
    "Application": "FundManagement.Api"
  }
}
```

Fallback when Serilog disabled: `builder.Logging.AddConsole()`.

---

## OpenTelemetry Configuration

### Packages

| Package | Purpose |
|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | `AddOpenTelemetry()` host integration |
| `OpenTelemetry.Instrumentation.AspNetCore` | HTTP request spans + metrics |
| `OpenTelemetry.Instrumentation.Http` | Outbound HttpClient spans + metrics |
| `OpenTelemetry.Instrumentation.Runtime` | .NET runtime metrics (GC, threadpool) |
| `OpenTelemetry.Instrumentation.Process` | Process metrics (CPU, memory) |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP gRPC/HTTP exporter |
| `Npgsql.OpenTelemetry` | Dapper/Npgsql DB spans |

### Wiring

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: "FundManagement.Api",
            serviceVersion: Assembly.GetExecutingAssembly()
                .GetName().Version?.ToString() ?? "unknown")
        .AddAttributes([
            new("deployment.environment", builder.Environment.EnvironmentName)
        ]))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation(o => o.RecordException = true)
        .AddNpgsql()
        .AddOtlpExporter(o => {
            o.Endpoint = new Uri(opts.Endpoint);
            o.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(opts.Endpoint)))
    .WithLogging(l => l
        .AddOtlpExporter(o => o.Endpoint = new Uri(opts.Endpoint)));
```

### Environment variable overrides (12-factor)

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://collector:4317
OTEL_SERVICE_NAME=FundManagement.Api
OTEL_RESOURCE_ATTRIBUTES=deployment.environment=production
```

OTel SDK resolves `OTEL_*` env vars automatically — no code change per environment.

---

## Azure Application Insights Configuration

### Packages

| Package | Purpose |
|---------|---------|
| `Microsoft.ApplicationInsights.AspNetCore` | Auto-instrumentation, Live Metrics, Smart Detection |

### Wiring

```csharp
builder.Services.AddApplicationInsightsTelemetry(o =>
    o.ConnectionString = opts.ConnectionString);
```

Auto-instruments: HTTP requests, outbound dependencies (HttpClient, SQL), exceptions, custom `ILogger` events. No additional code in handlers.

### Connection string

Placeholder in `appsettings.json`. Override per environment via:
- `appsettings.{Environment}.json` (gitignored)
- `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable (Azure App Service picks this up automatically)

---

## Files to Create / Modify

| File | Action |
|------|--------|
| `FundManagement.Api/ObservabilityOptions.cs` | Create — strongly-typed options |
| `FundManagement.Api/ObservabilityExtensions.cs` | Create — `AddObservability()` / `UseObservability()` + pillar methods |
| `FundManagement.Api/Program.cs` | Modify — bootstrap logger, `builder.AddObservability()`, `app.UseObservability()` |
| `FundManagement.Api/appsettings.json` | Modify — add `Observability` + `Serilog` sections |
| `FundManagement.Api/FundManagement.Api.csproj` | Modify — add all NuGet packages |

---

## Out of Scope

- File sink (Serilog) — add later by extending `WriteTo` in config
- Seq / Datadog / Grafana Cloud sink — add via config when needed
- Custom OTel ActivitySource for MediatR handlers — phase 2
- Alerting rules in App Insights / OTel backend — ops concern, not code
