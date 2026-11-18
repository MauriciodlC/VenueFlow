using VenueFlow.Domain.Venues;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Seeding;

public sealed class DemoVenuesTests
{
    [Fact]
    public void Build_returns_the_two_demo_venues_with_their_fixed_ids()
    {
        var venues = DemoVenues.Build();

        Assert.Equal(new[] { DemoVenues.RiversideStudioId, DemoVenues.HarborLoftId }, venues.Select(venue => venue.Id));
        Assert.All(venues, venue => Assert.NotEmpty(venue.Rooms));
    }

    [Fact]
    public void Build_gives_every_room_a_unique_id_linked_to_its_venue()
    {
        var venues = DemoVenues.Build();
        var rooms = venues.SelectMany(venue => venue.Rooms).ToList();

        Assert.Equal(rooms.Count, rooms.Select(room => room.Id).Distinct().Count());
        Assert.All(venues, venue => Assert.All(venue.Rooms, room => Assert.Equal(venue.Id, room.VenueId)));
        Assert.All(rooms, room => Assert.InRange(room.Capacity, 1, Room.MaxCapacity));
    }

    [Fact]
    public void Build_uses_iana_zones_with_and_without_daylight_saving_time()
    {
        var venues = DemoVenues.Build();
        var winter = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var summer = new DateTimeOffset(2027, 7, 15, 12, 0, 0, TimeSpan.Zero);

        foreach (var venue in venues)
        {
            Assert.Equal(venue.TimeZoneId, Venue.RequireIanaTimeZone(venue.TimeZoneId));
        }

        var observesDst = venues.Count(venue =>
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(venue.TimeZoneId);
            return zone.GetUtcOffset(winter) != zone.GetUtcOffset(summer);
        });

        Assert.Equal(1, observesDst);
    }

    [Fact]
    public void Build_returns_new_instances_on_each_call()
    {
        var first = DemoVenues.Build();
        var second = DemoVenues.Build();

        Assert.NotSame(first[0], second[0]);
        Assert.Equal(first.Select(venue => venue.Id), second.Select(venue => venue.Id));
    }

    [Fact]
    public void SelectMissing_skips_venues_already_in_the_database()
    {
        var demo = DemoVenues.Build();

        Assert.Equal(2, DemoSeeder.SelectMissing(demo, []).Count);

        var missing = Assert.Single(DemoSeeder.SelectMissing(demo, [DemoVenues.RiversideStudioId]));
        Assert.Equal(DemoVenues.HarborLoftId, missing.Id);

        Assert.Empty(DemoSeeder.SelectMissing(demo, [DemoVenues.RiversideStudioId, DemoVenues.HarborLoftId]));
    }
}
