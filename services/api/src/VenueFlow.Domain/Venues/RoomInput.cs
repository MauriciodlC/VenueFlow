namespace VenueFlow.Domain.Venues;

/// <summary>
/// One room as sent by the draft editor. <see cref="Id"/> is null for a new
/// room and the existing id for a room being kept or changed.
/// </summary>
public sealed record RoomInput(Guid? Id, string Name, int Capacity);
