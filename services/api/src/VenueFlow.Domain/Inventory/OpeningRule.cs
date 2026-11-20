namespace VenueFlow.Domain.Inventory;

/// <summary>
/// A weekly opening window for one room, in the venue's local wall-clock time:
/// every <see cref="Weekday"/> the room is bookable from <see cref="StartLocal"/>
/// to <see cref="EndLocal"/> in slots of <see cref="SlotMinutes"/>. A window
/// never crosses midnight; a late-night opening is two rules.
/// </summary>
public sealed class OpeningRule
{
    public const int MinSlotMinutes = 15;
    public const int MaxSlotMinutes = 720;

    /// <summary>Slot lengths and window edges must sit on this grid (minutes).</summary>
    public const int MinuteStep = 5;

    private OpeningRule()
    {
        // Used by EF Core.
    }

    internal OpeningRule(Guid id, Guid roomId, OpeningRuleInput input)
    {
        Validate(input);
        Id = id;
        RoomId = roomId;
        Weekday = input.Weekday;
        StartLocal = input.StartLocal;
        EndLocal = input.EndLocal;
        SlotMinutes = input.SlotMinutes;
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    public DayOfWeek Weekday { get; private set; }

    public TimeOnly StartLocal { get; private set; }

    public TimeOnly EndLocal { get; private set; }

    public int SlotMinutes { get; private set; }

    /// <summary>
    /// Local start times of the whole slots that fit in the window. A remainder
    /// shorter than one slot at the end of the window is not offered.
    /// </summary>
    public IEnumerable<TimeOnly> SlotStarts()
    {
        var length = TimeSpan.FromMinutes(SlotMinutes);
        for (var start = StartLocal.ToTimeSpan(); start + length <= EndLocal.ToTimeSpan(); start += length)
        {
            yield return TimeOnly.FromTimeSpan(start);
        }
    }

    internal bool Overlaps(OpeningRule other) =>
        Weekday == other.Weekday && StartLocal < other.EndLocal && other.StartLocal < EndLocal;

    internal static void Validate(OpeningRuleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Weekday))
        {
            throw new DomainException("weekday_invalid", "Weekday must be one of monday to sunday.");
        }

        if (!OnGrid(input.StartLocal) || !OnGrid(input.EndLocal))
        {
            throw new DomainException("opening_time_off_grid", $"Opening times must be whole minutes on a {MinuteStep}-minute step.");
        }

        if (input.EndLocal <= input.StartLocal)
        {
            throw new DomainException("opening_window_invalid", "An opening window must end after it starts on the same day.");
        }

        if (input.SlotMinutes is < MinSlotMinutes or > MaxSlotMinutes || input.SlotMinutes % MinuteStep != 0)
        {
            throw new DomainException(
                "slot_length_invalid",
                $"Slot length must be {MinSlotMinutes}-{MaxSlotMinutes} minutes in steps of {MinuteStep}.");
        }

        if ((input.EndLocal - input.StartLocal).TotalMinutes < input.SlotMinutes)
        {
            throw new DomainException("slot_longer_than_window", "The opening window is shorter than one slot.");
        }
    }

    internal static bool OnGrid(TimeOnly time) =>
        time.Second == 0 && time.Millisecond == 0 && time.Microsecond == 0 && time.Nanosecond == 0 && time.Minute % MinuteStep == 0;
}
