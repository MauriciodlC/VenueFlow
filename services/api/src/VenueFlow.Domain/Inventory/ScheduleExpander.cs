namespace VenueFlow.Domain.Inventory;

/// <summary>A bookable interval produced from a room's schedule. Not a reservation.</summary>
/// <param name="StartUtc">Start instant, always with a zero offset.</param>
/// <param name="EndUtc">End instant; exactly the rule's slot length after <paramref name="StartUtc"/>.</param>
/// <param name="UtcOffset">The venue's UTC offset for the whole slot.</param>
/// <param name="Capacity">Seats on sale after capacity-limit exceptions.</param>
/// <param name="CapacityLimited">True when an exception lowered the capacity.</param>
public sealed record SlotCandidate(
    Guid RuleId,
    DateOnly LocalDate,
    TimeOnly LocalStart,
    TimeOnly LocalEnd,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    TimeSpan UtcOffset,
    int Capacity,
    bool CapacityLimited);

/// <summary>A slot the weekly rules describe but that is not offered, and why.</summary>
public sealed record SkippedSlot(DateOnly LocalDate, TimeOnly LocalStart, TimeOnly LocalEnd, string Reason);

public sealed record ScheduleExpansion(IReadOnlyList<SlotCandidate> Slots, IReadOnlyList<SkippedSlot> Skipped);

/// <summary>
/// Turns weekly local-time rules into concrete UTC slots for a range of local
/// dates. Slots are defined by the wall clock at the venue; the conversion to
/// instants follows one policy so no two slots of a room ever overlap:
/// <list type="bullet">
/// <item>A start time that does not exist (the spring-forward gap) is skipped: <c>nonexistent_local_time</c>.</item>
/// <item>A start time that happens twice (the fall-back overlap) means its first occurrence, the earlier instant.</item>
/// <item>A slot keeps one UTC offset from start to end, so it lasts exactly its length in elapsed time and
/// its local end is its local start plus that length. A slot that a clock change would cut through is
/// skipped: <c>dst_transition_within_slot</c>. Ending exactly at the change is fine.</item>
/// <item>Closures remove every slot they overlap (<c>closed</c>); capacity limits lower the seats on sale, and
/// the lowest applicable limit wins.</item>
/// </list>
/// Because every slot sits inside one offset period, wall-clock order and UTC order agree and slots that
/// do not overlap on the wall clock do not overlap in time either.
/// The expander never reads bookings or holds; it only says what the schedule offers.
/// </summary>
public static class ScheduleExpander
{
    /// <summary>The longest range one call may expand, in days.</summary>
    public const int MaxDays = 62;

    public const string NonexistentLocalTime = "nonexistent_local_time";
    public const string TransitionWithinSlot = "dst_transition_within_slot";
    public const string Closed = "closed";

    public static ScheduleExpansion Expand(
        TimeZoneInfo zone,
        IEnumerable<OpeningRule> rules,
        IEnumerable<ScheduleException> exceptions,
        int roomCapacity,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(exceptions);
        ArgumentOutOfRangeException.ThrowIfLessThan(roomCapacity, 1);
        RequireRange(from, to);

        var rulesByDay = rules
            .OrderBy(rule => rule.StartLocal)
            .ToLookup(rule => rule.Weekday);
        var exceptionsByDate = exceptions.ToLookup(exception => exception.Date);

        var slots = new List<SlotCandidate>();
        var skipped = new List<SkippedSlot>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayExceptions = exceptionsByDate[date].ToList();
            foreach (var rule in rulesByDay[date.DayOfWeek])
            {
                var length = TimeSpan.FromMinutes(rule.SlotMinutes);
                foreach (var start in rule.SlotStarts())
                {
                    var end = start.Add(length);
                    var covering = dayExceptions.Where(exception => exception.Covers(date, start, end)).ToList();
                    if (covering.Any(exception => exception.Kind == ScheduleExceptionKind.Closure))
                    {
                        skipped.Add(new SkippedSlot(date, start, end, Closed));
                        continue;
                    }

                    var localStart = date.ToDateTime(start, DateTimeKind.Unspecified);
                    if (zone.IsInvalidTime(localStart))
                    {
                        skipped.Add(new SkippedSlot(date, start, end, NonexistentLocalTime));
                        continue;
                    }

                    var offset = OffsetOfFirstOccurrence(zone, localStart);
                    var startUtc = new DateTimeOffset(localStart - offset, TimeSpan.Zero);
                    var endUtc = startUtc + length;

                    // The offset must hold until the slot's last instant; a slot may end
                    // exactly when the clocks change, but not straddle the change.
                    if (zone.GetUtcOffset(endUtc - TimeSpan.FromTicks(1)) != offset)
                    {
                        skipped.Add(new SkippedSlot(date, start, end, TransitionWithinSlot));
                        continue;
                    }

                    var limits = covering
                        .Where(exception => exception.Kind == ScheduleExceptionKind.CapacityLimit)
                        .Select(exception => exception.Capacity!.Value)
                        .ToList();
                    var capacity = limits.Count == 0 ? roomCapacity : Math.Min(roomCapacity, limits.Min());

                    slots.Add(new SlotCandidate(
                        rule.Id,
                        date,
                        start,
                        end,
                        startUtc,
                        endUtc,
                        offset,
                        capacity,
                        capacity < roomCapacity));
                }
            }
        }

        return new ScheduleExpansion(slots.OrderBy(slot => slot.StartUtc).ToList(), skipped);
    }

    public static void RequireRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new DomainException("date_range_invalid", "The range must end on or after its start date.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
        {
            throw new DomainException("date_range_too_long", $"Ask for at most {MaxDays} days at a time.");
        }
    }

    /// <summary>
    /// The UTC offset for a valid local time. When the time happens twice, the
    /// larger offset gives the earlier instant, i.e. the first occurrence.
    /// </summary>
    private static TimeSpan OffsetOfFirstOccurrence(TimeZoneInfo zone, DateTime local) =>
        zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
}
