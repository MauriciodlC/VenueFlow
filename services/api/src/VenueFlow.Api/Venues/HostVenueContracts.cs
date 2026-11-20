using VenueFlow.Domain.Venues;

namespace VenueFlow.Api.Venues;

public sealed record CreateVenueDraftRequest(string? Name, string? TimeZoneId);

public sealed record RoomRequest(Guid? Id, string? Name, int Capacity);

/// <summary>
/// Full replacement of a draft's editable fields. <see cref="Rooms"/> null
/// leaves the rooms as they are; an empty list removes them all.
/// </summary>
public sealed record UpdateVenueDraftRequest(
    string? Name,
    string? TimeZoneId,
    string? Description,
    string? City,
    string? AddressLine,
    string? AccessibilityNotes,
    IReadOnlyList<RoomRequest>? Rooms);

public sealed record RoomResponse(Guid Id, string Name, int Capacity);

public sealed record VenueSummaryResponse(Guid Id, string Name, string? City, string Status, int RoomCount, int Revision);

public sealed record VenueDraftResponse(
    Guid Id,
    string Name,
    string TimeZoneId,
    string? Description,
    string? City,
    string? AddressLine,
    string? AccessibilityNotes,
    string Status,
    int Revision,
    IReadOnlyList<RoomResponse> Rooms,
    IReadOnlyList<string> Missing)
{
    public static VenueDraftResponse From(Venue venue) => new(
        venue.Id,
        venue.Name,
        venue.TimeZoneId,
        venue.Description,
        venue.City,
        venue.AddressLine,
        venue.AccessibilityNotes,
        venue.Status.ToString(),
        venue.Revision,
        HostVenueMapping.Rooms(venue),
        venue.MissingForSubmission());
}

/// <summary>
/// The listing as guests will see it once published. The street address is
/// left out: guests get it with their booking confirmation.
/// </summary>
public sealed record VenuePreviewResponse(
    Guid Id,
    string Name,
    string? Description,
    string? City,
    string TimeZoneId,
    string? AccessibilityNotes,
    int LargestRoomCapacity,
    IReadOnlyList<RoomResponse> Rooms,
    string Status,
    bool ReadyToSubmit,
    IReadOnlyList<string> Missing)
{
    public static VenuePreviewResponse From(Venue venue)
    {
        var missing = venue.MissingForSubmission();
        return new(
            venue.Id,
            venue.Name,
            venue.Description,
            venue.City,
            venue.TimeZoneId,
            venue.AccessibilityNotes,
            venue.Rooms.Count == 0 ? 0 : venue.Rooms.Max(room => room.Capacity),
            HostVenueMapping.Rooms(venue),
            venue.Status.ToString(),
            venue.Status == VenueStatus.Draft && missing.Count == 0,
            missing);
    }
}

internal static class HostVenueMapping
{
    public static IReadOnlyList<RoomResponse> Rooms(Venue venue) =>
        venue.Rooms
            .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .Select(room => new RoomResponse(room.Id, room.Name, room.Capacity))
            .ToList();
}
