using System.Net.Http.Headers;
using FundManagement.Api;
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
using Serilog;
using Serilog.Events;

// Bootstrap logger — catches startup failures before host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability();

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

    // Redis cache — falls back to in-memory when no connection string configured (local dev)
    var redisCs = builder.Configuration["Redis:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(redisCs))
        builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisCs);
    else
        builder.Services.AddDistributedMemoryCache();

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

    app.UseObservability();
    app.UseCors();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
