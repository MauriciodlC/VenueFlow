namespace VenueFlow.Domain.Venues;

/// <summary>
/// A physical venue owned by a host. All of its slots are interpreted in
/// <see cref="TimeZoneId"/>, which must be an IANA timezone (e.g. "Europe/Madrid").
/// </summary>
public sealed class Venue
{
    public const int NameMaxLength = 200;
    public const int TimeZoneIdMaxLength = 64;

    private readonly List<Room> _rooms = [];

    private Venue()
    {
        // Used by EF Core.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TimeZoneId { get; private set; } = string.Empty;

    public IReadOnlyCollection<Room> Rooms => _rooms.AsReadOnly();

    public static Venue Create(Guid id, string name, string timeZoneId)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("venue_id_required", "A venue needs a non-empty id.");
        }

        return new Venue
        {
            Id = id,
            Name = Text.Required(name, NameMaxLength, "venue_name"),
            TimeZoneId = RequireIanaTimeZone(timeZoneId),
        };
    }

    public Room AddRoom(Guid roomId, string name, int capacity)
    {
        var room = new Room(roomId, Id, name, capacity);
        if (_rooms.Any(existing => string.Equals(existing.Name, room.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("room_name_taken", $"Venue already has a room named '{room.Name}'.");
        }

        _rooms.Add(room);
        return room;
    }

    /// <summary>
    /// Accepts only IANA ids. Windows ids such as "Romance Standard Time" are
    /// rejected even where the runtime could convert them, so stored values are
    /// portable and match what the web client sends.
    /// </summary>
    public static string RequireIanaTimeZone(string? timeZoneId)
    {
        var trimmed = timeZoneId?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > TimeZoneIdMaxLength
            || !TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var zone)
            || !zone.HasIanaId)
        {
            throw new DomainException("time_zone_invalid", $"'{trimmed}' is not a known IANA timezone.");
        }

        return trimmed;
    }
}
