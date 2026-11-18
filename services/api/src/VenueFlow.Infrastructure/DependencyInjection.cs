using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVenueFlowPersistence(this IServiceCollection services, string postgresConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnectionString);

        services.AddDbContext<VenueFlowDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        return services;
    }
}
