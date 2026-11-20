namespace VenueFlow.Domain.Venues;

/// <summary>
/// A physical venue owned by a host. All of its slots are interpreted in
/// <see cref="TimeZoneId"/>, which must be an IANA timezone (e.g. "Europe/Madrid").
/// A venue starts as a <see cref="VenueStatus.Draft"/>; only drafts can be
/// edited, and only complete drafts can be submitted for review.
/// </summary>
public sealed class Venue
{
    public const int NameMaxLength = 200;
    public const int TimeZoneIdMaxLength = 64;
    public const int DescriptionMaxLength = 2000;
    public const int CityMaxLength = 120;
    public const int AddressLineMaxLength = 200;
    public const int AccessibilityNotesMaxLength = 1000;
    public const int MaxRooms = 50;

    private readonly List<Room> _rooms = [];

    private Venue()
    {
        // Used by EF Core.
    }

    public Guid Id { get; private set; }

    /// <summary>The host account that owns the venue. Never changes.</summary>
    public Guid HostId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TimeZoneId { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? City { get; private set; }

    public string? AddressLine { get; private set; }

    /// <summary>Step-free access, hearing loops, etc., kept exactly as the host wrote them.</summary>
    public string? AccessibilityNotes { get; private set; }

    public VenueStatus Status { get; private set; }

    /// <summary>
    /// Increases by one on every change. Used as the optimistic concurrency
    /// token and as the HTTP ETag, so two editors cannot overwrite each other.
    /// </summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<Room> Rooms => _rooms.AsReadOnly();

    public static Venue Create(Guid id, Guid hostId, string name, string timeZoneId)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("venue_id_required", "A venue needs a non-empty id.");
        }

        if (hostId == Guid.Empty)
        {
            throw new DomainException("venue_host_required", "A venue needs an owning host.");
        }

        return new Venue
        {
            Id = id,
            HostId = hostId,
            Name = Text.Required(name, NameMaxLength, "venue_name"),
            TimeZoneId = RequireIanaTimeZone(timeZoneId),
            Status = VenueStatus.Draft,
            Revision = 1,
        };
    }

    public bool IsOwnedBy(Guid userId) => userId != Guid.Empty && HostId == userId;

    /// <summary>
    /// Replaces the descriptive fields of a draft. Optional fields may be blank
    /// while drafting; <see cref="MissingForSubmission"/> lists what is still needed.
    /// </summary>
    public void UpdateDetails(
        string name,
        string timeZoneId,
        string? description,
        string? city,
        string? addressLine,
        string? accessibilityNotes) =>
        EditDraft(name, timeZoneId, description, city, addressLine, accessibilityNotes, rooms: null, newId: Guid.NewGuid);

    /// <summary>
    /// One editor save: details and, when <paramref name="rooms"/> is not null,
    /// the room list (see <see cref="ReplaceRooms"/>). Everything is validated
    /// before anything changes, and the revision goes up once.
    /// </summary>
    public void EditDraft(
        string name,
        string timeZoneId,
        string? description,
        string? city,
        string? addressLine,
        string? accessibilityNotes,
        IReadOnlyList<RoomInput>? rooms,
        Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);
        EnsureEditable();

        var newName = Text.Required(name, NameMaxLength, "venue_name");
        var newZone = RequireIanaTimeZone(timeZoneId);
        var newDescription = Text.Optional(description, DescriptionMaxLength, "description");
        var newCity = Text.Optional(city, CityMaxLength, "city");
        var newAddress = Text.Optional(addressLine, AddressLineMaxLength, "address_line");
        var newAccessibility = Text.Optional(accessibilityNotes, AccessibilityNotesMaxLength, "accessibility_notes");
        if (rooms is not null)
        {
            ValidateRooms(rooms);
        }

        Name = newName;
        TimeZoneId = newZone;
        Description = newDescription;
        City = newCity;
        AddressLine = newAddress;
        AccessibilityNotes = newAccessibility;
        if (rooms is not null)
        {
            ApplyRooms(rooms, newId);
        }

        Touch();
    }

    public Room AddRoom(Guid roomId, string name, int capacity)
    {
        EnsureEditable();
        if (_rooms.Count >= MaxRooms)
        {
            throw new DomainException("too_many_rooms", $"A venue can have at most {MaxRooms} rooms.");
        }

        var room = new Room(roomId, Id, name, capacity);
        if (_rooms.Any(existing => string.Equals(existing.Name, room.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("room_name_taken", $"Venue already has a room named '{room.Name}'.");
        }

        _rooms.Add(room);
        Touch();
        return room;
    }

    /// <summary>
    /// Makes the draft's rooms match <paramref name="rooms"/>: entries with an
    /// id update that room, entries without one are added with a new id, and
    /// rooms that are not listed are removed. Nothing changes if any entry is invalid.
    /// </summary>
    public void ReplaceRooms(IReadOnlyList<RoomInput> rooms, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);
        EnsureEditable();
        ValidateRooms(rooms);
        ApplyRooms(rooms, newId);
        Touch();
    }

    /// <summary>
    /// Field codes still needed before the draft can be submitted, in form
    /// order. Empty when the listing is complete.
    /// </summary>
    public IReadOnlyList<string> MissingForSubmission()
    {
        var missing = new List<string>();
        if (Description is null)
        {
            missing.Add("description");
        }

        if (City is null)
        {
            missing.Add("city");
        }

        if (AddressLine is null)
        {
            missing.Add("address_line");
        }

        if (_rooms.Count == 0)
        {
            missing.Add("rooms");
        }

        return missing;
    }

    /// <summary>Host hands a complete draft to an admin for review.</summary>
    public void SubmitForReview()
    {
        RequireStatus(VenueStatus.Draft, VenueStatus.InReview);
        var missing = MissingForSubmission();
        if (missing.Count > 0)
        {
            throw new DomainException("venue_incomplete", $"Complete these fields before submitting: {string.Join(", ", missing)}.");
        }

        Status = VenueStatus.InReview;
        Touch();
    }

    /// <summary>Admin approves a listing in review; guests can now find it.</summary>
    public void Approve()
    {
        RequireStatus(VenueStatus.InReview, VenueStatus.Published);
        Status = VenueStatus.Published;
        Touch();
    }

    /// <summary>Sends a listing in review back to the host for changes.</summary>
    public void ReturnToDraft()
    {
        RequireStatus(VenueStatus.InReview, VenueStatus.Draft);
        Status = VenueStatus.Draft;
        Touch();
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

    private void ValidateRooms(IReadOnlyList<RoomInput> rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        if (rooms.Count > MaxRooms)
        {
            throw new DomainException("too_many_rooms", $"A venue can have at most {MaxRooms} rooms.");
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<Guid>();
        foreach (var input in rooms)
        {
            // Building a throwaway room runs the same name and capacity rules.
            var probe = new Room(input.Id ?? Guid.NewGuid(), Id, input.Name, input.Capacity);
            if (!seenNames.Add(probe.Name))
            {
                throw new DomainException("room_name_taken", $"Room name '{probe.Name}' is used more than once.");
            }

            if (input.Id is { } id && (!seenIds.Add(id) || _rooms.All(existing => existing.Id != id)))
            {
                throw new DomainException("room_not_found", $"Room '{id}' does not belong to this venue.");
            }
        }
    }

    private void ApplyRooms(IReadOnlyList<RoomInput> rooms, Func<Guid> newId)
    {
        var keep = rooms.Where(input => input.Id is not null).Select(input => input.Id!.Value).ToHashSet();
        _rooms.RemoveAll(existing => !keep.Contains(existing.Id));

        foreach (var input in rooms)
        {
            if (input.Id is { } id)
            {
                var room = _rooms.Single(existing => existing.Id == id);
                room.Rename(input.Name);
                room.ChangeCapacity(input.Capacity);
            }
            else
            {
                _rooms.Add(new Room(newId(), Id, input.Name, input.Capacity));
            }
        }
    }

    private void EnsureEditable()
    {
        if (Status != VenueStatus.Draft)
        {
            throw new DomainException("venue_not_editable", $"Only drafts can be edited; this venue is {Status}.");
        }
    }

    private void RequireStatus(VenueStatus expected, VenueStatus target)
    {
        if (Status != expected)
        {
            throw new DomainException("venue_status_transition_invalid", $"A venue cannot move from {Status} to {target}.");
        }
    }

    private void Touch() => Revision++;
}
