using Microsoft.EntityFrameworkCore;
using VenueFlow.Domain.Venues;
using VenueFlow.Infrastructure.Accounts;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Infrastructure.Seeding;

/// <summary>Outcome of one seed run.</summary>
public sealed record SeedResult(int AccountsAdded, int AccountsAlreadyPresent, int VenuesAdded, int VenuesAlreadyPresent);

/// <summary>
/// Inserts the demo accounts and venues that are not in the database yet.
/// Running it twice changes nothing; rows that already exist are left as they
/// are (including their passwords), so to start over reset the database
/// (<c>scripts/dev.sh reset</c>) and seed again.
/// </summary>
public static class DemoSeeder
{
    public static async Task<SeedResult> SeedAsync(
        VenueFlowDbContext db,
        AccountPasswords passwords,
        string? demoPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(passwords);

        var accounts = DemoAccounts.Build();
        var accountIds = accounts.Select(account => account.Id).ToList();
        var existingAccounts = await db.Users
            .Where(account => accountIds.Contains(account.Id))
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);
        var newAccounts = accounts.Where(account => !existingAccounts.Contains(account.Id)).ToList();

        if (newAccounts.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(demoPassword))
            {
                throw new InvalidOperationException(
                    "Demo:Password is not set. scripts/dev.sh setup generates one in infra/compose/.env (DEMO_PASSWORD).");
            }

            foreach (var account in newAccounts)
            {
                passwords.Set(account, demoPassword);
            }

            db.Users.AddRange(newAccounts);
        }

        var demo = DemoVenues.Build();
        var ids = demo.Select(venue => venue.Id).ToList();
        var existing = await db.Venues
            .Where(venue => ids.Contains(venue.Id))
            .Select(venue => venue.Id)
            .ToListAsync(cancellationToken);

        var missing = SelectMissing(demo, existing);
        db.Venues.AddRange(missing);

        if (newAccounts.Count > 0 || missing.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new SeedResult(
            newAccounts.Count,
            accounts.Count - newAccounts.Count,
            missing.Count,
            demo.Count - missing.Count);
    }

    /// <summary>Returns the venues whose id is not in <paramref name="existingIds"/>, in seed order.</summary>
    public static IReadOnlyList<Venue> SelectMissing(IEnumerable<Venue> demo, IEnumerable<Guid> existingIds)
    {
        ArgumentNullException.ThrowIfNull(demo);
        ArgumentNullException.ThrowIfNull(existingIds);

        var present = existingIds.ToHashSet();
        return demo.Where(venue => !present.Contains(venue.Id)).ToList();
    }
}
