namespace VenueFlow.Domain.Inventory;

/// <summary>
/// A slot that holds and bookings can point at. Candidates from
/// <see cref="ScheduleExpander"/> become rows here when capacity is first
/// claimed, so the table only grows with real demand. One row per room and
/// start instant; <see cref="Version"/> increases on every change and is the
/// concurrency token the hold transaction checks (capacity invariant 1).
/// </summary>
public sealed class InventorySlot
{
    private InventorySlot()
    {
        // Used by EF Core.
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    public DateTimeOffset StartUtc { get; private set; }

    public DateTimeOffset EndUtc { get; private set; }

    /// <summary>Seats on sale when the slot was materialised (room capacity or an exception's limit).</summary>
    public int TotalCapacity { get; private set; }

    public int Version { get; private set; }

    public static InventorySlot FromCandidate(Guid id, Guid roomId, SlotCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (id == Guid.Empty || roomId == Guid.Empty)
        {
            throw new DomainException("slot_id_required", "A slot needs an id and a room.");
        }

        if (candidate.StartUtc.Offset != TimeSpan.Zero || candidate.EndUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainException("slot_not_utc", "Slot instants are stored in UTC.");
        }

        if (candidate.EndUtc <= candidate.StartUtc)
        {
            throw new DomainException("slot_interval_invalid", "A slot must end after it starts.");
        }

        if (candidate.Capacity < 1)
        {
            throw new DomainException("slot_capacity_invalid", "A slot needs at least one seat.");
        }

        return new InventorySlot
        {
            Id = id,
            RoomId = roomId,
            StartUtc = candidate.StartUtc,
            EndUtc = candidate.EndUtc,
            TotalCapacity = candidate.Capacity,
            Version = 1,
        };
    }
}
