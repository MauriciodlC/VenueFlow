using VenueFlow.Domain.Venues;

namespace VenueFlow.Domain.Inventory;

/// <summary>
/// A one-off change to a room's weekly rules on one local date: a closure, or
/// a lower capacity limit. Without <see cref="StartLocal"/>/<see cref="EndLocal"/>
/// it covers the whole day; otherwise every slot that overlaps the window.
/// </summary>
public sealed class ScheduleException
{
    public const int NoteMaxLength = 200;

    private ScheduleException()
    {
        // Used by EF Core.
    }

    internal ScheduleException(Guid id, Guid roomId, ScheduleExceptionInput input)
    {
        Validate(input);
        Id = id;
        RoomId = roomId;
        Date = input.Date;
        StartLocal = input.StartLocal;
        EndLocal = input.EndLocal;
        Kind = input.Kind;
        Capacity = input.Capacity;
        Note = Text.Optional(input.Note, NoteMaxLength, "exception_note");
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    /// <summary>The venue-local calendar date the exception applies to.</summary>
    public DateOnly Date { get; private set; }

    public TimeOnly? StartLocal { get; private set; }

    public TimeOnly? EndLocal { get; private set; }

    public ScheduleExceptionKind Kind { get; private set; }

    /// <summary>Seats still sold while a <see cref="ScheduleExceptionKind.CapacityLimit"/> applies.</summary>
    public int? Capacity { get; private set; }

    public string? Note { get; private set; }

    public bool IsWholeDay => StartLocal is null;

    /// <summary>True when the exception touches the local slot [start, end) on <paramref name="date"/>.</summary>
    public bool Covers(DateOnly date, TimeOnly start, TimeOnly end) =>
        date == Date && (IsWholeDay || (start < EndLocal!.Value && StartLocal!.Value < end));

    internal static void Validate(ScheduleExceptionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Kind))
        {
            throw new DomainException("exception_kind_invalid", "Exception kind must be closure or capacity_limit.");
        }

        if (input.StartLocal.HasValue != input.EndLocal.HasValue)
        {
            throw new DomainException("exception_window_incomplete", "Give both start and end times, or neither for the whole day.");
        }

        if (input.StartLocal is { } start && input.EndLocal is { } end)
        {
            if (!OpeningRule.OnGrid(start) || !OpeningRule.OnGrid(end))
            {
                throw new DomainException(
                    "exception_time_off_grid",
                    $"Exception times must be whole minutes on a {OpeningRule.MinuteStep}-minute step.");
            }

            if (end <= start)
            {
                throw new DomainException("exception_window_invalid", "An exception window must end after it starts on the same day.");
            }
        }

        switch (input.Kind)
        {
            case ScheduleExceptionKind.Closure when input.Capacity is not null:
                throw new DomainException("exception_capacity_not_allowed", "A closure has no capacity.");
            case ScheduleExceptionKind.CapacityLimit when input.Capacity is not (>= 1 and <= Room.MaxCapacity):
                throw new DomainException(
                    "exception_capacity_invalid",
                    $"A capacity limit needs a capacity between 1 and {Room.MaxCapacity}; use a closure to stop sales.");
        }
    }
}
