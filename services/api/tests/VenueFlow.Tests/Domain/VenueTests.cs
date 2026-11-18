using VenueFlow.Domain;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Tests.Domain;

public sealed class VenueTests
{
    private static readonly Guid VenueId = Guid.Parse("0b1c6f1e-4d1a-4f7e-9a51-6f0f3c1a2b01");

    [Theory]
    [InlineData("Europe/Madrid")]
    [InlineData("America/Bogota")]
    [InlineData("America/New_York")]
    [InlineData("UTC")]
    public void Create_accepts_iana_time_zones(string timeZoneId)
    {
        var venue = Venue.Create(VenueId, "  Studio North  ", timeZoneId);

        Assert.Equal(VenueId, venue.Id);
        Assert.Equal("Studio North", venue.Name);
        Assert.Equal(timeZoneId, venue.TimeZoneId);
        Assert.Empty(venue.Rooms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Romance Standard Time")]
    [InlineData("+02:00")]
    public void Create_rejects_unknown_or_windows_time_zones(string timeZoneId)
    {
        var error = Assert.Throws<DomainException>(() => Venue.Create(VenueId, "Studio North", timeZoneId));

        Assert.Equal("time_zone_invalid", error.Code);
    }

    [Fact]
    public void Create_rejects_empty_id_and_blank_or_long_names()
    {
        Assert.Equal("venue_id_required",
            Assert.Throws<DomainException>(() => Venue.Create(Guid.Empty, "Studio", "UTC")).Code);
        Assert.Equal("venue_name_required",
            Assert.Throws<DomainException>(() => Venue.Create(VenueId, " ", "UTC")).Code);
        Assert.Equal("venue_name_too_long",
            Assert.Throws<DomainException>(() => Venue.Create(VenueId, new string('a', Venue.NameMaxLength + 1), "UTC")).Code);
    }

    [Fact]
    public void AddRoom_links_room_to_venue()
    {
        var venue = Venue.Create(VenueId, "Studio North", "Europe/Madrid");
        var roomId = Guid.Parse("7d3e2a90-1f4b-4c2e-8a7d-2b9f6c4e1d02");

        var room = venue.AddRoom(roomId, "Loft", 12);

        Assert.Equal(roomId, room.Id);
        Assert.Equal(VenueId, room.VenueId);
        Assert.Equal(12, room.Capacity);
        Assert.Same(room, Assert.Single(venue.Rooms));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(Room.MaxCapacity + 1)]
    public void AddRoom_rejects_capacity_outside_range(int capacity)
    {
        var venue = Venue.Create(VenueId, "Studio North", "UTC");

        var error = Assert.Throws<DomainException>(() => venue.AddRoom(Guid.NewGuid(), "Loft", capacity));

        Assert.Equal("room_capacity_invalid", error.Code);
        Assert.Empty(venue.Rooms);
    }

    [Fact]
    public void AddRoom_rejects_duplicate_names_ignoring_case_and_spaces()
    {
        var venue = Venue.Create(VenueId, "Studio North", "UTC");
        venue.AddRoom(Guid.NewGuid(), "Loft", 12);

        var error = Assert.Throws<DomainException>(() => venue.AddRoom(Guid.NewGuid(), " loft ", 4));

        Assert.Equal("room_name_taken", error.Code);
        Assert.Single(venue.Rooms);
    }
}
