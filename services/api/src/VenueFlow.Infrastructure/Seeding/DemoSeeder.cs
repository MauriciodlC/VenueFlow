using Microsoft.EntityFrameworkCore;
using VenueFlow.Domain.Venues;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Infrastructure.Seeding;

/// <summary>Outcome of one seed run.</summary>
public sealed record SeedResult(int VenuesAdded, int VenuesAlreadyPresent);

/// <summary>
/// Inserts the demo venues that are not in the database yet. Running it twice
/// changes nothing; rows that already exist are left as they are, so to start
/// over reset the database (<c>docker compose down -v</c>) and seed again.
/// </summary>
public static class DemoSeeder
{
    public static async Task<SeedResult> SeedAsync(VenueFlowDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        var demo = DemoVenues.Build();
        var ids = demo.Select(venue => venue.Id).ToList();
        var existing = await db.Venues
            .Where(venue => ids.Contains(venue.Id))
            .Select(venue => venue.Id)
            .ToListAsync(cancellationToken);

        var missing = SelectMissing(demo, existing);
        if (missing.Count > 0)
        {
            db.Venues.AddRange(missing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new SeedResult(missing.Count, demo.Count - missing.Count);
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
