using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using VenueFlow.Api.Health;
using VenueFlow.Infrastructure;
using VenueFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

builder.Services.AddVenueFlowPersistence(postgres);
builder.Services.AddOpenApi();

// Readiness covers the database (source of truth) and Redis (advisory cache).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VenueFlowDbContext>(name: "database", tags: [HealthTags.Ready])
    .AddRedis(redis, name: "redis", tags: [HealthTags.Ready], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness runs no checks: it only proves the process is serving requests.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponse.WriteAsync,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthTags.Ready),
    ResponseWriter = HealthResponse.WriteAsync,
});

app.MapGet("/api/info", () => new ServiceInfo("venueflow-api", ServiceInfo.CurrentVersion))
    .WithName("GetServiceInfo")
    .WithSummary("Service name and version")
    .Produces<ServiceInfo>();

app.Run();

/// <summary>Basic service metadata used by the web client and smoke checks.</summary>
public sealed record ServiceInfo(string Name, string Version)
{
    public static string CurrentVersion { get; } =
        typeof(ServiceInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

public partial class Program;
