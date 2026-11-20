using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Seeding;

/// <summary>
/// Synthetic venues for local development and demos. The ids are fixed so the
/// seed is idempotent and docs, tests and demo scripts can point at the same rows.
/// One venue observes daylight-saving time and one uses a fixed UTC offset, so
/// both cases are covered once schedules arrive. Both belong to the demo host
/// and are published, so guests can find them.
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
        var riverside = Venue.Create(RiversideStudioId, DemoAccounts.HostId, "Riverside Makers Studio", "Europe/Madrid");
        riverside.UpdateDetails(
            "Riverside Makers Studio",
            "Europe/Madrid",
            "Ground-floor workshop studio by the river with long benches and natural light.",
            "Madrid",
            "Paseo de la Ribera 12 (synthetic address)",
            "Step-free entrance from the street. Accessible toilet on the same floor.");
        riverside.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e11"), "Workshop Hall", 24);
        riverside.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e12"), "Quiet Room", 8);

        // America/Bogota: UTC-05:00 all year.
        var harbor = Venue.Create(HarborLoftId, DemoAccounts.HostId, "Harbor Coworking Loft", "America/Bogota");
        harbor.UpdateDetails(
            "Harbor Coworking Loft",
            "America/Bogota",
            "Open-plan loft above the harbor with a meeting pod for small groups.",
            "Bogota",
            "Carrera del Puerto 45, piso 3 (synthetic address)",
            "Lift to the third floor. No step-free access to the roof terrace.");
        harbor.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e21"), "Main Loft", 40);
        harbor.AddRoom(Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e22"), "Meeting Pod", 6);

        foreach (var venue in new[] { riverside, harbor })
        {
            venue.SubmitForReview();
            venue.Approve();
        }

        return [riverside, harbor];
    }
}
