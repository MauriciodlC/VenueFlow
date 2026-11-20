using VenueFlow.Domain;
using VenueFlow.Domain.Inventory;
using static VenueFlow.Tests.Domain.ScheduleExpanderTests;

namespace VenueFlow.Tests.Domain;

public sealed class RoomScheduleTests
{
    [Fact]
    public void A_new_schedule_is_empty_at_revision_zero()
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());

        Assert.Equal(0, schedule.Revision);
        Assert.Empty(schedule.Rules);
        Assert.Empty(schedule.Exceptions);
    }

    [Fact]
    public void Replace_stores_rules_and_exceptions_and_bumps_the_revision_once()
    {
        var roomId = Guid.NewGuid();
        var schedule = RoomSchedule.Empty(roomId);

        schedule.Replace(
            [Rule(DayOfWeek.Tuesday, "09:00", "12:00", 60), Rule(DayOfWeek.Monday, "14:00", "18:00", 30), Rule(DayOfWeek.Monday, "09:00", "12:00", 60)],
            [Change("2026-12-25", null, null, ScheduleExceptionKind.Closure, null)],
            Guid.NewGuid);

        Assert.Equal(1, schedule.Revision);
        Assert.Equal(
            new[] { (DayOfWeek.Monday, new TimeOnly(9, 0)), (DayOfWeek.Monday, new TimeOnly(14, 0)), (DayOfWeek.Tuesday, new TimeOnly(9, 0)) },
            schedule.Rules.Select(rule => (rule.Weekday, rule.StartLocal)));
        Assert.All(schedule.Rules, rule => Assert.Equal(roomId, rule.RoomId));
        var closure = Assert.Single(schedule.Exceptions);
        Assert.True(closure.IsWholeDay);
        Assert.Equal(roomId, closure.RoomId);
    }

    [Fact]
    public void Replacing_again_drops_the_previous_rules()
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        schedule.Replace([Rule(DayOfWeek.Monday, "09:00", "12:00", 60)], [], Guid.NewGuid);

        schedule.Replace([], [], Guid.NewGuid);

        Assert.Empty(schedule.Rules);
        Assert.Equal(2, schedule.Revision);
    }

    [Theory]
    [InlineData("12:00", "09:00", 60, "opening_window_invalid")]
    [InlineData("09:00", "09:00", 60, "opening_window_invalid")]
    [InlineData("09:00", "09:30", 60, "slot_longer_than_window")]
    [InlineData("09:00", "18:00", 10, "slot_length_invalid")]
    [InlineData("09:00", "18:00", 62, "slot_length_invalid")]
    [InlineData("09:00", "23:00", 725, "slot_length_invalid")]
    [InlineData("09:02", "12:00", 60, "opening_time_off_grid")]
    public void Invalid_rules_are_rejected(string start, string end, int slotMinutes, string code)
    {
        AssertRejected(code, [Rule(DayOfWeek.Monday, start, end, slotMinutes)], []);
    }

    [Fact]
    public void An_unknown_weekday_is_rejected()
    {
        AssertRejected("weekday_invalid", [Rule((DayOfWeek)7, "09:00", "12:00", 60)], []);
    }

    [Fact]
    public void Overlapping_windows_on_the_same_weekday_are_rejected_but_touching_ones_are_fine()
    {
        AssertRejected(
            "opening_rules_overlap",
            [Rule(DayOfWeek.Friday, "09:00", "13:00", 60), Rule(DayOfWeek.Friday, "12:00", "18:00", 60)],
            []);

        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        schedule.Replace(
            [Rule(DayOfWeek.Friday, "09:00", "13:00", 60), Rule(DayOfWeek.Friday, "13:00", "18:00", 60), Rule(DayOfWeek.Saturday, "09:00", "13:00", 60)],
            [],
            Guid.NewGuid);
        Assert.Equal(3, schedule.Rules.Count);
    }

    [Theory]
    [InlineData("10:00", null, ScheduleExceptionKind.Closure, null, "exception_window_incomplete")]
    [InlineData("12:00", "10:00", ScheduleExceptionKind.Closure, null, "exception_window_invalid")]
    [InlineData("10:01", "12:00", ScheduleExceptionKind.Closure, null, "exception_time_off_grid")]
    [InlineData(null, null, ScheduleExceptionKind.Closure, 5, "exception_capacity_not_allowed")]
    [InlineData(null, null, ScheduleExceptionKind.CapacityLimit, null, "exception_capacity_invalid")]
    [InlineData(null, null, ScheduleExceptionKind.CapacityLimit, 0, "exception_capacity_invalid")]
    [InlineData(null, null, ScheduleExceptionKind.CapacityLimit, 1001, "exception_capacity_invalid")]
    [InlineData(null, null, (ScheduleExceptionKind)9, null, "exception_kind_invalid")]
    public void Invalid_exceptions_are_rejected(string? start, string? end, ScheduleExceptionKind kind, int? capacity, string code)
    {
        AssertRejected(code, [], [Change("2026-12-24", start, end, kind, capacity)]);
    }

    [Fact]
    public void Too_many_rules_or_exceptions_are_rejected()
    {
        var rules = Enumerable.Range(0, RoomSchedule.MaxRules + 1)
            .Select(i => Rule(DayOfWeek.Monday, $"{i % 24:00}:00", $"{i % 24:00}:30", 15))
            .ToList();
        var exceptions = Enumerable.Range(0, RoomSchedule.MaxExceptions + 1)
            .Select(i => new ScheduleExceptionInput(new DateOnly(2026, 1, 1).AddDays(i), null, null, ScheduleExceptionKind.Closure, null, null))
            .ToList();

        AssertRejected("too_many_rules", rules, []);
        AssertRejected("too_many_exceptions", [], exceptions);
    }

    [Fact]
    public void A_rejected_replace_leaves_the_schedule_unchanged()
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        schedule.Replace([Rule(DayOfWeek.Monday, "09:00", "12:00", 60)], [], Guid.NewGuid);
        var before = schedule.Rules.Single();

        Assert.Throws<DomainException>(() => schedule.Replace(
            [Rule(DayOfWeek.Monday, "08:00", "10:00", 60), Rule(DayOfWeek.Monday, "09:00", "11:00", 60)],
            [],
            Guid.NewGuid));

        Assert.Same(before, Assert.Single(schedule.Rules));
        Assert.Equal(1, schedule.Revision);
    }

    [Fact]
    public void Exception_notes_are_trimmed_and_limited()
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        schedule.Replace(
            [],
            [new ScheduleExceptionInput(new DateOnly(2026, 12, 31), null, null, ScheduleExceptionKind.Closure, null, "  New Year's Eve  ")],
            Guid.NewGuid);
        Assert.Equal("New Year's Eve", schedule.Exceptions.Single().Note);

        AssertRejected(
            "exception_note_too_long",
            [],
            [new ScheduleExceptionInput(new DateOnly(2026, 12, 31), null, null, ScheduleExceptionKind.Closure, null, new string('x', ScheduleException.NoteMaxLength + 1))]);
    }

    [Fact]
    public void Slot_starts_fill_the_window_with_whole_slots()
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        schedule.Replace([Rule(DayOfWeek.Monday, "09:00", "10:40", 25)], [], Guid.NewGuid);

        Assert.Equal(
            new[] { new TimeOnly(9, 0), new TimeOnly(9, 25), new TimeOnly(9, 50), new TimeOnly(10, 15) },
            schedule.Rules.Single().SlotStarts());
    }

    private static void AssertRejected(string code, IReadOnlyList<OpeningRuleInput> rules, IReadOnlyList<ScheduleExceptionInput> exceptions)
    {
        var schedule = RoomSchedule.Empty(Guid.NewGuid());
        var error = Assert.Throws<DomainException>(() => schedule.Replace(rules, exceptions, Guid.NewGuid));
        Assert.Equal(code, error.Code);
        Assert.Equal(0, schedule.Revision);
    }
}
