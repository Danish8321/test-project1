using System.Reflection;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Serilog;
using Serilog.Events;

namespace FundManagement.Api;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var opts = builder.Configuration
            .GetSection("Observability")
            .Get<ObservabilityOptions>() ?? new();

        if (opts.Serilog.Enabled)
            builder.ConfigureSerilog();
        else
            builder.Logging.AddConsole();

        if (opts.OpenTelemetry.Enabled)
            builder.ConfigureOpenTelemetry(opts.OpenTelemetry);

        if (opts.ApplicationInsights.Enabled)
            builder.ConfigureAppInsights(opts.ApplicationInsights);

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

    private static void ConfigureSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, config) => config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId());
    }

    private static void ConfigureOpenTelemetry(this WebApplicationBuilder builder, OpenTelemetryOptions opts)
    {
        var endpoint = new Uri(opts.Endpoint);
        var protocol = opts.Protocol.Equals("Http", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(
                    serviceName: "FundManagement.Api",
                    serviceVersion: Assembly.GetExecutingAssembly()
                        .GetName().Version?.ToString() ?? "unknown")
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
                ]))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                .AddHttpClientInstrumentation(o => o.RecordException = true)
                .AddNpgsql()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = protocol;
                }))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(o => { o.Endpoint = endpoint; o.Protocol = protocol; }))
            .WithLogging(l => l
                .AddOtlpExporter(o => { o.Endpoint = endpoint; o.Protocol = protocol; }));
    }

    private static void ConfigureAppInsights(this WebApplicationBuilder builder, ApplicationInsightsOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            return;

        builder.Services.AddApplicationInsightsTelemetry(o =>
            o.ConnectionString = opts.ConnectionString);
    }
}
