using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using VenueFlow.Api.Inventory;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Api;

/// <summary>
/// Room listing, schedule editing and slot listing for hosts, on the seeded
/// demo data: Riverside (Europe/Madrid) and Harbor (America/Bogota) belong to
/// host@example.test and come with weekly opening rules.
/// </summary>
public sealed class RoomScheduleEndpointTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private const string Host = "host@example.test";
    private const string OtherHost = "other-host@example.test";

    private static readonly Guid Riverside = DemoVenues.RiversideStudioId;
    private static readonly Guid Harbor = DemoVenues.HarborLoftId;

    public static TheoryData<string> NonHostAccounts => new() { "guest@example.test", "staff@example.test", "admin@example.test" };

    [Fact]
    public async Task Rooms_are_listed_with_a_summary_of_their_schedules()
    {
        using var host = await factory.SignedInClientAsync(Host);

        var rooms = await host.GetFromJsonAsync<List<HostRoomResponse>>($"/api/host/venues/{Riverside}/rooms");

        Assert.Equal(
            new[]
            {
                new HostRoomResponse(DemoSchedules.QuietRoomId, "Quiet Room", 8, 5, 0, 1),
                new HostRoomResponse(DemoSchedules.WorkshopHallId, "Workshop Hall", 24, 6, 2, 1),
            },
            rooms!);
    }

    [Fact]
    public async Task The_seeded_schedule_is_returned_in_local_time_with_its_etag()
    {
        using var host = await factory.SignedInClientAsync(Host);

        using var response = await host.GetAsync(SchedulePath(Riverside, DemoSchedules.WorkshopHallId));
        var schedule = await response.Content.ReadFromJsonAsync<RoomScheduleResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag!.ToString());
        Assert.Equal(("Europe/Madrid", "Workshop Hall", 24, 1), (schedule!.TimeZoneId, schedule.RoomName, schedule.RoomCapacity, schedule.Revision));
        Assert.Equal(
            new[] { "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" },
            schedule.Rules.Select(rule => rule.Weekday));
        Assert.Equal(("09:00", "18:00", 60), (schedule.Rules[0].StartLocal, schedule.Rules[0].EndLocal, schedule.Rules[0].SlotMinutes));
        Assert.Equal(
            new[] { ("2026-12-24", "capacity_limit", (int?)10), ("2026-12-25", "closure", (int?)null) },
            schedule.Exceptions.Select(exception => (exception.Date, exception.Kind, exception.Capacity)));
    }

    [Fact]
    public async Task A_room_without_a_schedule_starts_at_revision_zero_and_the_first_save_creates_it()
    {
        using var host = await factory.SignedInClientAsync(Host);
        var (venueId, _) = await host.CreateCompleteDraftAsync("Schedule newcomer");
        var roomId = (await host.ReadVenueAsync(venueId)).Venue.Rooms.Single().Id;

        using var empty = await host.GetAsync(SchedulePath(venueId, roomId));
        Assert.Equal("\"0\"", empty.Headers.ETag!.ToString());
        Assert.Empty((await empty.Content.ReadFromJsonAsync<RoomScheduleResponse>())!.Rules);

        using var saved = await host.PutScheduleAsync(venueId, roomId, WeekdayMornings, "\"0\"");
        var schedule = await saved.Content.ReadFromJsonAsync<RoomScheduleResponse>();

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("\"1\"", saved.Headers.ETag!.ToString());
        Assert.Equal(new[] { "monday", "wednesday" }, schedule!.Rules.Select(rule => rule.Weekday));
        var closure = Assert.Single(schedule.Exceptions);
        Assert.Equal(("2026-04-01", "10:00", "11:00", "closure", "Plumbing repair"), (closure.Date, closure.StartLocal, closure.EndLocal, closure.Kind, closure.Note));

        var reread = await host.GetFromJsonAsync<RoomScheduleResponse>(SchedulePath(venueId, roomId));
        Assert.Equal(schedule.Rules, reread!.Rules);
        Assert.Equal(schedule.Exceptions, reread.Exceptions);
    }

    [Fact]
    public async Task Saving_needs_if_match_and_a_stale_etag_is_refused()
    {
        using var host = await factory.SignedInClientAsync(Host);
        var path = SchedulePath(Harbor, DemoSchedules.MeetingPodId);
        using var current = await host.GetAsync(path);
        var etag = current.Headers.ETag!.ToString();

        using var missing = await host.PutScheduleAsync(Harbor, DemoSchedules.MeetingPodId, WeekdayMornings, ifMatch: null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal("if_match_required", await missing.ProblemCodeAsync());

        // Published venues can change their hours; the listing itself stays read-only.
        using var first = await host.PutScheduleAsync(Harbor, DemoSchedules.MeetingPodId, WeekdayMornings, etag);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await host.PutScheduleAsync(Harbor, DemoSchedules.MeetingPodId, new { rules = Array.Empty<object>() }, etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("revision_mismatch", await stale.ProblemCodeAsync());

        var after = await host.GetFromJsonAsync<RoomScheduleResponse>(path);
        Assert.Equal(2, after!.Rules.Count);
    }

    [Theory]
    [InlineData("funday", "09:00", "12:00", 60, "weekday_invalid")]
    [InlineData("monday", "9am", "12:00", 60, "start_local_invalid")]
    [InlineData("monday", "09:00", "24:00", 60, "end_local_invalid")]
    [InlineData("monday", "12:00", "09:00", 60, "opening_window_invalid")]
    [InlineData("monday", "09:00", "12:00", 7, "slot_length_invalid")]
    public async Task Invalid_rules_are_rejected_and_the_schedule_is_unchanged(string weekday, string start, string end, int slotMinutes, string code)
    {
        using var host = await factory.SignedInClientAsync(Host);
        var path = SchedulePath(Riverside, DemoSchedules.QuietRoomId);
        using var before = await host.GetAsync(path);
        var etag = before.Headers.ETag!.ToString();

        var body = new { rules = new[] { new { weekday, startLocal = start, endLocal = end, slotMinutes } } };
        using var response = await host.PutScheduleAsync(Riverside, DemoSchedules.QuietRoomId, body, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
        using var after = await host.GetAsync(path);
        Assert.Equal(etag, after.Headers.ETag!.ToString());
    }

    [Theory]
    [InlineData("holiday", null, "exception_kind_invalid")]
    [InlineData("capacity_limit", null, "exception_capacity_invalid")]
    [InlineData("closure", 4, "exception_capacity_not_allowed")]
    public async Task Invalid_exceptions_are_rejected(string kind, int? capacity, string code)
    {
        using var host = await factory.SignedInClientAsync(Host);
        using var before = await host.GetAsync(SchedulePath(Riverside, DemoSchedules.QuietRoomId));

        var body = new { exceptions = new[] { new { date = "2026-12-31", kind, capacity } } };
        using var response = await host.PutScheduleAsync(Riverside, DemoSchedules.QuietRoomId, body, before.Headers.ETag!.ToString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Slots_follow_the_weekly_rules_closures_and_capacity_limits()
    {
        using var host = await factory.SignedInClientAsync(Host);

        // Monday 21 to Sunday 27 December 2026 (CET, +01:00).
        var slots = await host.GetFromJsonAsync<RoomSlotsResponse>(
            $"{SlotsPath(Riverside, DemoSchedules.WorkshopHallId)}?from=2026-12-21&to=2026-12-27");

        Assert.Equal(("Europe/Madrid", "2026-12-21", "2026-12-27"), (slots!.TimeZoneId, slots.From, slots.To));
        Assert.Equal(38, slots.Slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 12, 21, 8, 0, 0, TimeSpan.Zero), slots.Slots[0].StartUtc);
        Assert.All(slots.Slots, slot => Assert.Equal("+01:00", slot.UtcOffset));
        Assert.DoesNotContain(slots.Slots, slot => slot.LocalDate == "2026-12-25");
        Assert.Equal(9, slots.Skipped.Count(skipped => skipped.LocalDate == "2026-12-25" && skipped.Reason == "closed"));

        var christmasEve = slots.Slots.Where(slot => slot.LocalDate == "2026-12-24").ToList();
        Assert.Equal(
            new[] { 24, 24, 24, 24, 24, 10, 10, 10, 10 },
            christmasEve.Select(slot => slot.Capacity));
        Assert.Equal(new[] { "10:00", "12:00" }, slots.Slots.Where(slot => slot.LocalDate == "2026-12-26").Select(slot => slot.LocalStart));
    }

    [Fact]
    public async Task A_fixed_offset_venue_lists_slots_in_its_own_offset()
    {
        using var host = await factory.SignedInClientAsync(Host);

        // Sunday 1 November 2026: New York changes its clocks, Bogota does not.
        var slots = await host.GetFromJsonAsync<RoomSlotsResponse>(
            $"{SlotsPath(Harbor, DemoSchedules.MainLoftId)}?from=2026-10-31&to=2026-11-02");

        Assert.Equal(new[] { "2026-10-31", "2026-11-02" }, slots!.Slots.Select(slot => slot.LocalDate).Distinct());
        Assert.All(slots.Slots, slot => Assert.Equal("-05:00", slot.UtcOffset));
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 13, 0, 0, TimeSpan.Zero), slots.Slots[0].StartUtc);
        Assert.Empty(slots.Skipped);
    }

    [Fact]
    public async Task Without_dates_the_coming_week_is_listed()
    {
        using var host = await factory.SignedInClientAsync(Host);

        var slots = await host.GetFromJsonAsync<RoomSlotsResponse>(SlotsPath(Riverside, DemoSchedules.WorkshopHallId));

        var from = DateOnly.Parse(slots!.From, System.Globalization.CultureInfo.InvariantCulture);
        var to = DateOnly.Parse(slots.To, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(6, to.DayNumber - from.DayNumber);
    }

    [Theory]
    [InlineData("?from=2026-03-10&to=2026-03-09", "date_range_invalid")]
    [InlineData("?from=2026-03-01&to=2026-05-02", "date_range_too_long")]
    [InlineData("?from=2026-13-01", "from_invalid")]
    [InlineData("?from=2026-03-01&to=tomorrow", "to_invalid")]
    public async Task Invalid_slot_ranges_are_rejected(string query, string code)
    {
        using var host = await factory.SignedInClientAsync(Host);

        using var response = await host.GetAsync(SlotsPath(Riverside, DemoSchedules.WorkshopHallId) + query);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Another_host_gets_404_on_every_route_and_cannot_change_the_schedule()
    {
        using var owner = await factory.SignedInClientAsync(Host);
        using var intruder = await factory.SignedInClientAsync(OtherHost);
        using var before = await owner.GetAsync(SchedulePath(Riverside, DemoSchedules.WorkshopHallId));

        foreach (var call in EveryRoute(Riverside, DemoSchedules.WorkshopHallId))
        {
            using var response = await call(intruder);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("venue_not_found", await response.ProblemCodeAsync());
        }

        using var after = await owner.GetAsync(SchedulePath(Riverside, DemoSchedules.WorkshopHallId));
        Assert.Equal(before.Headers.ETag!.ToString(), after.Headers.ETag!.ToString());
    }

    [Fact]
    public async Task A_room_must_belong_to_the_venue_in_the_path()
    {
        using var host = await factory.SignedInClientAsync(Host);

        // The Main Loft is Harbor's, not Riverside's, even though both are the host's.
        foreach (var call in EveryRoute(Riverside, DemoSchedules.MainLoftId).Skip(1))
        {
            using var response = await call(host);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("room_not_found", await response.ProblemCodeAsync());
        }
    }

    [Theory]
    [MemberData(nameof(NonHostAccounts))]
    public async Task Non_host_roles_are_forbidden(string email)
    {
        using var client = await factory.SignedInClientAsync(email);

        foreach (var call in EveryRoute(Riverside, DemoSchedules.WorkshopHallId))
        {
            using var response = await call(client);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Signed_out_callers_are_unauthorized()
    {
        using var client = factory.CreateClient();

        foreach (var call in EveryRoute(Riverside, DemoSchedules.WorkshopHallId))
        {
            using var response = await call(client);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static readonly object WeekdayMornings = new
    {
        rules = new[]
        {
            new { weekday = "wednesday", startLocal = "08:00", endLocal = "12:00", slotMinutes = 60 },
            new { weekday = "monday", startLocal = "08:00", endLocal = "12:00", slotMinutes = 60 },
        },
        exceptions = new[]
        {
            new { date = "2026-04-01", startLocal = "10:00", endLocal = "11:00", kind = "closure", note = "Plumbing repair" },
        },
    };

    private static string SchedulePath(Guid venueId, Guid roomId) => $"/api/host/venues/{venueId}/rooms/{roomId}/schedule";

    private static string SlotsPath(Guid venueId, Guid roomId) => $"/api/host/venues/{venueId}/rooms/{roomId}/slots";

    private static IEnumerable<Func<HttpClient, Task<HttpResponseMessage>>> EveryRoute(Guid venueId, Guid roomId) =>
    [
        client => client.GetAsync($"/api/host/venues/{venueId}/rooms"),
        client => client.GetAsync(SchedulePath(venueId, roomId)),
        client => client.PutScheduleAsync(venueId, roomId, new { rules = Array.Empty<object>() }, "*"),
        client => client.GetAsync(SlotsPath(venueId, roomId) + "?from=2026-12-21&to=2026-12-27"),
    ];
}

internal static class ScheduleClientExtensions
{
    public static async Task<HttpResponseMessage> PutScheduleAsync(this HttpClient client, Guid venueId, Guid roomId, object body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/host/venues/{venueId}/rooms/{roomId}/schedule")
        {
            Content = JsonContent.Create(body),
        };
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return await client.SendAsync(request);
    }
}
