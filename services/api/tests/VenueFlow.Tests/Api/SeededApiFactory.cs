using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using VenueFlow.Infrastructure.Accounts;
using VenueFlow.Infrastructure.Persistence;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Api;

/// <summary>
/// Runs the API against an EF Core in-memory database holding the demo seed,
/// so auth and listing endpoints can be exercised without PostgreSQL. Database
/// constraints are not enforced here; the schema tests cover those.
/// </summary>
public sealed class SeededApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "test-only-demo-password";

    private readonly string _databaseName = $"venueflow-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", "Host=127.0.0.1;Port=1;Database=venueflow_test;Username=test;Password=test");
        builder.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1,abortConnect=false,connectTimeout=200");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<VenueFlowDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VenueFlowDbContext>>();
            services.AddDbContext<VenueFlowDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueFlowDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<AccountPasswords>();
        DemoSeeder.SeedAsync(db, passwords, DemoPassword).GetAwaiter().GetResult();

        return host;
    }
}
