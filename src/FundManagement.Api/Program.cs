using System.Net.Http.Headers;
using FundManagement.Api.Endpoints;
using FundManagement.Application.Behaviours;
using FundManagement.Application.Common;
using FundManagement.Infrastructure.Circle;
using FundManagement.Infrastructure.Data;
using FundManagement.Infrastructure.Migrations;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new DbConnectionFactory(connectionString));

// Circle HTTP client
builder.Services.AddHttpClient<ICircleClient, CircleClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Circle:BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["Circle:ApiKey"]);
});

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ICircleClient).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()));

var app = builder.Build();

// Run migrations on startup
new MigrationRunner(connectionString).Run();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();

// Endpoints
app.MapHealthEndpoints();

app.Run();
