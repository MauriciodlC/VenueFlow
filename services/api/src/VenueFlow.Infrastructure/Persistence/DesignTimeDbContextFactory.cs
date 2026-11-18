using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VenueFlow.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without starting the API. The
/// connection string comes from <c>VENUEFLOW_POSTGRES</c>; it is only opened by
/// commands that touch the database (e.g. <c>database update</c>).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VenueFlowDbContext>
{
    public VenueFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("VENUEFLOW_POSTGRES")
            ?? "Host=127.0.0.1;Port=5432;Database=venueflow;Username=venueflow";

        var options = new DbContextOptionsBuilder<VenueFlowDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new VenueFlowDbContext(options);
    }
}
