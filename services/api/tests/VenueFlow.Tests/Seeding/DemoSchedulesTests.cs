using VenueFlow.Domain.Inventory;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Seeding;

public sealed class DemoSchedulesTests
{
    [Fact]
    public void Every_demo_room_has_a_schedule_and_no_schedule_points_elsewhere()
    {
        var roomIds = DemoVenues.Build().SelectMany(venue => venue.Rooms).Select(room => room.Id).Order();

        Assert.Equal(roomIds, DemoSchedules.Build().Keys.Order());
    }

    [Fact]
    public void Demo_schedules_are_saved_once_and_keyed_by_their_room()
    {
        foreach (var (roomId, schedule) in DemoSchedules.Build())
        {
            Assert.Equal(roomId, schedule.RoomId);
            Assert.Equal(1, schedule.Revision);
            Assert.NotEmpty(schedule.Rules);
        }
    }

    [Fact]
    public void Demo_rooms_offer_slots_in_a_normal_week()
    {
        var venues = DemoVenues.Build();
        var schedules = DemoSchedules.Build();

        foreach (var venue in venues)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(venue.TimeZoneId);
            foreach (var room in venue.Rooms)
            {
                var schedule = schedules[room.Id];
                var week = ScheduleExpander.Expand(zone, schedule.Rules, schedule.Exceptions, room.Capacity, new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 29));
                Assert.NotEmpty(week.Slots);
                Assert.Empty(week.Skipped);
            }
        }
    }
}
