using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Seeding;

/// <summary>
/// Synthetic venues for local development and demos. The ids are fixed so the
/// seed is idempotent and docs, tests and demo scripts can point at the same rows.
/// One venue observes daylight-saving time and one uses a fixed UTC offset, so
/// both cases are covered once schedules arrive.
/// </summary>
public static class DemoVenues
{
    public static readonly Guid RiversideStudioId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e01");

    public static readonly Guid HarborLoftId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e02");

    /// <summary>
    /// Builds new aggregates on every call, so each caller can hand them to its
    /// own <c>DbContext</c>. Everything goes through the domain factories, which
    /// means the seed obeys the same rules as data entered by hosts.
    /// </summary>
    public static IReadOnlyList<Venue> Build()
    {
        // Europe/Madrid: CET in winter, CEST in summer.
        var riverside = Venue.Create(RiversideStudioId, "Riverside Makers Studio", "Europe/Madrid");
        riverside.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e11"), "Workshop Hall", 24);
        riverside.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e12"), "Quiet Room", 8);

        // America/Bogota: UTC-05:00 all year.
        var harbor = Venue.Create(HarborLoftId, "Harbor Coworking Loft", "America/Bogota");
        harbor.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e21"), "Main Loft", 40);
        harbor.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e22"), "Meeting Pod", 6);

        return [riverside, harbor];
    }
}
