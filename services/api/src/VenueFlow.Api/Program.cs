using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using VenueFlow.Api.Auth;
using VenueFlow.Api.Health;
using VenueFlow.Api.Inventory;
using VenueFlow.Api.Venues;
using VenueFlow.Infrastructure;
using VenueFlow.Infrastructure.Accounts;
using VenueFlow.Infrastructure.Persistence;
using VenueFlow.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

var postgres = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
var redis = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

builder.Services.AddVenueFlowPersistence(postgres);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Session cookie for the Vue app (same origin through the Vite proxy). API
// callers get 401/403 instead of the default redirects to a login page.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "venueflow.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(AuthPolicies.Add);

// Readiness covers the database (source of truth) and Redis (advisory cache).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VenueFlowDbContext>(name: "database", tags: [HealthTags.Ready])
    .AddRedis(redis, name: "redis", tags: [HealthTags.Ready], timeout: TimeSpan.FromSeconds(3));

var app = builder.Build();

// `dotnet run -- seed` applies pending migrations, inserts the demo accounts and
// venues and exits without serving requests. Safe to run repeatedly. New demo
// accounts get the password from Demo:Password (DEMO_PASSWORD in the local .env).
if (args.Contains("seed", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<VenueFlowDbContext>();
    await db.Database.MigrateAsync();
    var passwords = scope.ServiceProvider.GetRequiredService<AccountPasswords>();
    var seeded = await DemoSeeder.SeedAsync(db, passwords, app.Configuration["Demo:Password"]);
    app.Logger.LogInformation(
        "Demo seed finished: {AccountsAdded} account(s) and {VenuesAdded} venue(s) added; {AccountsPresent} account(s) and {VenuesPresent} venue(s) already present.",
        seeded.AccountsAdded,
        seeded.VenuesAdded,
        seeded.AccountsAlreadyPresent,
        seeded.VenuesAlreadyPresent);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

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

app.MapAuthEndpoints();
app.MapHostVenueEndpoints();
app.MapRoomScheduleEndpoints();

await app.RunAsync();

/// <summary>Basic service metadata used by the web client and smoke checks.</summary>
public sealed record ServiceInfo(string Name, string Version)
{
    public static string CurrentVersion { get; } =
        typeof(ServiceInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}

public partial class Program;
