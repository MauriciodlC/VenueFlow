namespace VenueFlow.Domain.Venues;

/// <summary>
/// A bookable room inside a venue. <see cref="Capacity"/> is the most people
/// the room may hold at once; the booking engine never sells beyond it.
/// </summary>
public sealed class Room
{
    public const int NameMaxLength = 120;
    public const int MaxCapacity = 1000;

    private Room()
    {
        // Used by EF Core.
    }

    internal Room(Guid id, Guid venueId, string name, int capacity)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("room_id_required", "A room needs a non-empty id.");
        }

        if (capacity is < 1 or > MaxCapacity)
        {
            throw new DomainException("room_capacity_invalid", $"Room capacity must be between 1 and {MaxCapacity}.");
        }

        Id = id;
        VenueId = venueId;
        Name = Text.Required(name, NameMaxLength, "room_name");
        Capacity = capacity;
    }

    public Guid Id { get; private set; }

    public Guid VenueId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Capacity { get; private set; }
}
