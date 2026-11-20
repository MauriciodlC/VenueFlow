namespace VenueFlow.Domain.Inventory;

/// <summary>What a schedule exception does to the slots it covers. Stored by name.</summary>
public enum ScheduleExceptionKind
{
    /// <summary>No slots are offered.</summary>
    Closure,

    /// <summary>Slots are offered with at most <see cref="ScheduleException.Capacity"/> seats.</summary>
    CapacityLimit,
}
