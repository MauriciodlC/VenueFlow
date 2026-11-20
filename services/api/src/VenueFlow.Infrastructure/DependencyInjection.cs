using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VenueFlow.Domain.Accounts;
using VenueFlow.Infrastructure.Accounts;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVenueFlowPersistence(this IServiceCollection services, string postgresConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnectionString);

        services.AddDbContext<VenueFlowDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")));

        services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddSingleton<AccountPasswords>();

        return services;
    }
}
