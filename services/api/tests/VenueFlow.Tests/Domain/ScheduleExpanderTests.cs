using VenueFlow.Domain;
using VenueFlow.Domain.Inventory;

namespace VenueFlow.Tests.Domain;

/// <summary>
/// Recurrence expansion against real IANA zone rules. The DST fixtures use the
/// 2026 changes: Europe/Madrid springs forward 02:00→03:00 on 29 March and falls
/// back 03:00→02:00 on 25 October; America/New_York springs forward on 8 March
/// and falls back 02:00→01:00 on 1 November. Asia/Kolkata (+05:30) and
/// America/Bogota (-05:00) never change.
/// </summary>
public sealed class ScheduleExpanderTests
{
    private const int RoomCapacity = 20;

    [Fact]
    public void A_regular_week_produces_one_slot_per_step_on_matching_weekdays_only()
    {
        // Monday 23 March 2026 to Sunday 29 March; the rule is for Mondays only.
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Monday, "09:00", "12:00", 60)], [], "2026-03-23", "2026-03-29");

        Assert.Equal(
            new[] { "2026-03-23T08:00:00Z", "2026-03-23T09:00:00Z", "2026-03-23T10:00:00Z" },
            expansion.Slots.Select(slot => Iso(slot.StartUtc)));
        Assert.All(expansion.Slots, slot => Assert.Equal(TimeSpan.FromHours(1), slot.UtcOffset));
        Assert.Empty(expansion.Skipped);
    }

    [Fact]
    public void The_same_local_hours_move_in_utc_after_the_spring_change()
    {
        var before = Expand("Europe/Madrid", [Rule(DayOfWeek.Monday, "09:00", "10:00", 60)], [], "2026-03-23", "2026-03-23");
        var after = Expand("Europe/Madrid", [Rule(DayOfWeek.Monday, "09:00", "10:00", 60)], [], "2026-03-30", "2026-03-30");

        Assert.Equal("2026-03-23T08:00:00Z", Iso(Assert.Single(before.Slots).StartUtc));
        Assert.Equal("2026-03-30T07:00:00Z", Iso(Assert.Single(after.Slots).StartUtc));
        Assert.Equal(TimeSpan.FromHours(2), after.Slots[0].UtcOffset);
    }

    [Fact]
    public void Spring_forward_skips_nonexistent_starts_and_keeps_slots_that_end_at_the_change()
    {
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Sunday, "00:00", "05:00", 60)], [], "2026-03-29", "2026-03-29");

        Assert.Equal(
            new[]
            {
                ("00:00", "01:00", "2026-03-28T23:00:00Z", "2026-03-29T00:00:00Z", 1),
                ("01:00", "02:00", "2026-03-29T00:00:00Z", "2026-03-29T01:00:00Z", 1),
                ("03:00", "04:00", "2026-03-29T01:00:00Z", "2026-03-29T02:00:00Z", 2),
                ("04:00", "05:00", "2026-03-29T02:00:00Z", "2026-03-29T03:00:00Z", 2),
            },
            expansion.Slots.Select(Describe));
        var skipped = Assert.Single(expansion.Skipped);
        Assert.Equal((new TimeOnly(2, 0), ScheduleExpander.NonexistentLocalTime), (skipped.LocalStart, skipped.Reason));
    }

    [Fact]
    public void Spring_forward_with_half_hour_slots_skips_the_whole_gap()
    {
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Sunday, "01:00", "04:00", 30)], [], "2026-03-29", "2026-03-29");

        Assert.Equal(new[] { "01:00", "01:30", "03:00", "03:30" }, expansion.Slots.Select(slot => Hm(slot.LocalStart)));
        Assert.Equal(new[] { "02:00", "02:30" }, expansion.Skipped.Select(skipped => Hm(skipped.LocalStart)));
        Assert.All(expansion.Skipped, skipped => Assert.Equal(ScheduleExpander.NonexistentLocalTime, skipped.Reason));
        AssertNoOverlaps(expansion);
    }

    [Fact]
    public void Fall_back_resolves_the_repeated_hour_to_its_first_occurrence()
    {
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Sunday, "01:00", "05:00", 60)], [], "2026-10-25", "2026-10-25");

        Assert.Equal(
            new[]
            {
                ("01:00", "02:00", "2026-10-24T23:00:00Z", "2026-10-25T00:00:00Z", 2),
                ("02:00", "03:00", "2026-10-25T00:00:00Z", "2026-10-25T01:00:00Z", 2),
                ("03:00", "04:00", "2026-10-25T02:00:00Z", "2026-10-25T03:00:00Z", 1),
                ("04:00", "05:00", "2026-10-25T03:00:00Z", "2026-10-25T04:00:00Z", 1),
            },
            expansion.Slots.Select(Describe));
        Assert.Empty(expansion.Skipped);
        AssertNoOverlaps(expansion);
    }

    [Fact]
    public void A_slot_the_fall_back_change_would_cut_through_is_skipped()
    {
        var expansion = Expand("America/New_York", [Rule(DayOfWeek.Sunday, "00:30", "03:30", 60)], [], "2026-11-01", "2026-11-01");

        Assert.Equal(
            new[]
            {
                ("00:30", "01:30", "2026-11-01T04:30:00Z", "2026-11-01T05:30:00Z", -4),
                ("02:30", "03:30", "2026-11-01T07:30:00Z", "2026-11-01T08:30:00Z", -5),
            },
            expansion.Slots.Select(Describe));
        var skipped = Assert.Single(expansion.Skipped);
        Assert.Equal((new TimeOnly(1, 30), ScheduleExpander.TransitionWithinSlot), (skipped.LocalStart, skipped.Reason));
    }

    [Fact]
    public void A_long_slot_may_end_exactly_at_the_spring_change()
    {
        var expansion = Expand("America/New_York", [Rule(DayOfWeek.Sunday, "00:00", "04:00", 120)], [], "2026-03-08", "2026-03-08");

        Assert.Equal(
            new[] { ("00:00", "02:00", "2026-03-08T05:00:00Z", "2026-03-08T07:00:00Z", -5) },
            expansion.Slots.Select(Describe));
        var skipped = Assert.Single(expansion.Skipped);
        Assert.Equal((new TimeOnly(2, 0), ScheduleExpander.NonexistentLocalTime), (skipped.LocalStart, skipped.Reason));
        Assert.All(expansion.Slots, slot => Assert.Equal(TimeSpan.FromMinutes(120), slot.EndUtc - slot.StartUtc));
    }

    [Fact]
    public void A_long_slot_that_would_straddle_the_spring_change_is_skipped_not_shortened()
    {
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Sunday, "01:00", "05:00", 120)], [], "2026-03-29", "2026-03-29");

        Assert.Equal(
            new[] { ("03:00", "05:00", "2026-03-29T01:00:00Z", "2026-03-29T03:00:00Z", 2) },
            expansion.Slots.Select(Describe));
        var skipped = Assert.Single(expansion.Skipped);
        Assert.Equal((new TimeOnly(1, 0), ScheduleExpander.TransitionWithinSlot), (skipped.LocalStart, skipped.Reason));
    }

    [Theory]
    [InlineData("Asia/Kolkata", "2026-03-29", "01:00", "2026-03-28T19:30:00Z", 330)]
    [InlineData("America/Bogota", "2026-11-01", "08:00", "2026-11-01T13:00:00Z", -300)]
    public void Fixed_offset_zones_are_unaffected_by_other_zones_changes(string zone, string date, string firstStart, string firstUtc, int offsetMinutes)
    {
        var day = DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture).DayOfWeek;
        var expansion = Expand(zone, [Rule(day, firstStart, "12:00", 60)], [], date, date);

        Assert.Equal(firstUtc, Iso(expansion.Slots[0].StartUtc));
        Assert.All(expansion.Slots, slot => Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), slot.UtcOffset));
        Assert.Empty(expansion.Skipped);
        AssertNoOverlaps(expansion);
    }

    [Fact]
    public void A_whole_day_closure_removes_every_slot_that_day()
    {
        var expansion = Expand(
            "Europe/Madrid",
            [Rule(DayOfWeek.Monday, "09:00", "12:00", 60)],
            [Change("2026-03-23", null, null, ScheduleExceptionKind.Closure, null)],
            "2026-03-23",
            "2026-03-30");

        Assert.All(expansion.Slots, slot => Assert.Equal(new DateOnly(2026, 3, 30), slot.LocalDate));
        Assert.Equal(3, expansion.Slots.Count);
        Assert.Equal(3, expansion.Skipped.Count(skipped => skipped.Reason == ScheduleExpander.Closed));
    }

    [Fact]
    public void A_partial_closure_removes_only_the_slots_it_overlaps()
    {
        var expansion = Expand(
            "Europe/Madrid",
            [Rule(DayOfWeek.Monday, "09:00", "13:00", 60)],
            [Change("2026-03-23", "10:30", "11:15", ScheduleExceptionKind.Closure, null)],
            "2026-03-23",
            "2026-03-23");

        Assert.Equal(new[] { "09:00", "12:00" }, expansion.Slots.Select(slot => Hm(slot.LocalStart)));
        Assert.Equal(new[] { "10:00", "11:00" }, expansion.Skipped.Select(skipped => Hm(skipped.LocalStart)));
    }

    [Fact]
    public void Capacity_limits_lower_the_seats_and_the_lowest_limit_wins()
    {
        var expansion = Expand(
            "Europe/Madrid",
            [Rule(DayOfWeek.Monday, "09:00", "12:00", 60)],
            [
                Change("2026-03-23", "10:00", "12:00", ScheduleExceptionKind.CapacityLimit, 12),
                Change("2026-03-23", "11:00", "11:30", ScheduleExceptionKind.CapacityLimit, 5),
                Change("2026-03-23", null, null, ScheduleExceptionKind.CapacityLimit, 500),
            ],
            "2026-03-23",
            "2026-03-23");

        Assert.Equal(
            new[] { (RoomCapacity, false), (12, true), (5, true) },
            expansion.Slots.Select(slot => (slot.Capacity, slot.CapacityLimited)));
    }

    [Fact]
    public void A_closure_beats_a_capacity_limit_on_the_same_slot()
    {
        var expansion = Expand(
            "Europe/Madrid",
            [Rule(DayOfWeek.Monday, "09:00", "10:00", 60)],
            [
                Change("2026-03-23", null, null, ScheduleExceptionKind.CapacityLimit, 4),
                Change("2026-03-23", "09:00", "09:15", ScheduleExceptionKind.Closure, null),
            ],
            "2026-03-23",
            "2026-03-23");

        Assert.Empty(expansion.Slots);
        Assert.Equal(ScheduleExpander.Closed, Assert.Single(expansion.Skipped).Reason);
    }

    [Fact]
    public void A_remainder_shorter_than_one_slot_is_not_offered()
    {
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Monday, "09:00", "11:30", 60)], [], "2026-03-23", "2026-03-23");

        Assert.Equal(new[] { "09:00", "10:00" }, expansion.Slots.Select(slot => Hm(slot.LocalStart)));
    }

    [Fact]
    public void Slots_from_several_rules_are_returned_in_utc_order_without_overlaps()
    {
        var expansion = Expand(
            "America/New_York",
            [
                Rule(DayOfWeek.Sunday, "13:00", "17:00", 45),
                Rule(DayOfWeek.Sunday, "00:00", "03:00", 30),
                Rule(DayOfWeek.Saturday, "22:00", "23:55", 25),
            ],
            [],
            "2026-10-31",
            "2026-11-01");

        Assert.Equal(expansion.Slots.OrderBy(slot => slot.StartUtc), expansion.Slots);
        AssertNoOverlaps(expansion);
        Assert.All(expansion.Slots, slot => Assert.Equal(slot.StartUtc + (slot.LocalEnd - slot.LocalStart), slot.EndUtc));
    }

    [Theory]
    [InlineData("2026-03-10", "2026-03-09", "date_range_invalid")]
    [InlineData("2026-03-01", "2026-05-02", "date_range_too_long")]
    public void Invalid_ranges_are_rejected(string from, string to, string code)
    {
        var error = Assert.Throws<DomainException>(() => Expand("Europe/Madrid", [], [], from, to));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void The_longest_allowed_range_is_accepted()
    {
        var expansion = Expand("Europe/Madrid", [Rule(DayOfWeek.Monday, "09:00", "10:00", 60)], [], "2026-03-01", "2026-05-01");

        Assert.Equal(9, expansion.Slots.Count);
    }

    internal static OpeningRuleInput Rule(DayOfWeek weekday, string start, string end, int slotMinutes) =>
        new(weekday, TimeOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), TimeOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), slotMinutes);

    internal static ScheduleExceptionInput Change(string date, string? start, string? end, ScheduleExceptionKind kind, int? capacity) =>
        new(
            DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture),
            start is null ? null : TimeOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
            end is null ? null : TimeOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
            kind,
            capacity,
            Note: null);

    private static ScheduleExpansion Expand(
        string zoneId,
        IReadOnlyList<OpeningRuleInput> rules,
        IReadOnlyList<ScheduleExceptionInput> exceptions,
        string from,
        string to)
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        schedule.Replace(rules, exceptions, Guid.NewGuid);
        return ScheduleExpander.Expand(
            TimeZoneInfo.FindSystemTimeZoneById(zoneId),
            schedule.Rules,
            schedule.Exceptions,
            RoomCapacity,
            DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void AssertNoOverlaps(ScheduleExpansion expansion)
    {
        var ordered = expansion.Slots.OrderBy(slot => slot.StartUtc).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.True(ordered[i - 1].EndUtc <= ordered[i].StartUtc, $"{ordered[i - 1]} overlaps {ordered[i]}");
        }
    }

    private static (string Start, string End, string StartUtc, string EndUtc, int OffsetHours) Describe(SlotCandidate slot) =>
        (Hm(slot.LocalStart), Hm(slot.LocalEnd), Iso(slot.StartUtc), Iso(slot.EndUtc), (int)slot.UtcOffset.TotalHours);

    private static string Hm(TimeOnly time) => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static string Iso(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
