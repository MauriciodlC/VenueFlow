using Microsoft.EntityFrameworkCore;
using VenueFlow.Api.Auth;
using VenueFlow.Api.Http;
using VenueFlow.Domain;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Api.Inventory;

/// <summary>
/// Rooms and their schedules for the host who owns the venue. Like the venue
/// routes, every lookup is scoped to the caller: another host's venue answers
/// 404 <c>venue_not_found</c>, and a room of a different venue 404
/// <c>room_not_found</c>. Schedules can be edited in any listing status, since
/// opening hours change while a venue is live.
/// </summary>
internal static class RoomScheduleEndpoints
{
    /// <summary>Days shown when the caller does not pass <c>to</c>.</summary>
    public const int DefaultDays = 7;

    public static void MapRoomScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var rooms = app.MapGroup("/api/host/venues/{venueId:guid}/rooms")
            .WithTags("Host inventory")
            .RequireAuthorization(AuthPolicies.Host);

        rooms.MapGet("/", ListRoomsAsync)
            .WithName("ListHostRooms")
            .WithSummary("The venue's rooms with a summary of each schedule")
            .Produces<IReadOnlyList<HostRoomResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rooms.MapGet("/{roomId:guid}/schedule", GetScheduleAsync)
            .WithName("GetRoomSchedule")
            .WithSummary("Weekly opening rules and dated exceptions, in the venue's local time, with an ETag")
            .Produces<RoomScheduleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        rooms.MapPut("/{roomId:guid}/schedule", PutScheduleAsync)
            .WithName("ReplaceRoomSchedule")
            .WithSummary("Replace the room's rules and exceptions; requires If-Match")
            .Produces<RoomScheduleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        rooms.MapGet("/{roomId:guid}/slots", ListSlotsAsync)
            .WithName("ListRoomSlots")
            .WithSummary("Slot candidates for local dates from..to (inclusive, at most 62 days); not a reservation")
            .Produces<RoomSlotsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ListRoomsAsync(Guid venueId, HttpContext http, VenueFlowDbContext db, CancellationToken cancellationToken)
    {
        var venue = await FindOwnedVenueAsync(db, http, venueId, cancellationToken);
        if (venue is null)
        {
            return ApiProblems.NotFound("venue");
        }

        var roomIds = venue.Rooms.Select(room => room.Id).ToList();
        var summaries = await db.RoomSchedules
            .AsNoTracking()
            .Where(schedule => roomIds.Contains(schedule.RoomId))
            .Select(schedule => new { schedule.RoomId, Rules = schedule.Rules.Count(), Exceptions = schedule.Exceptions.Count(), schedule.Revision })
            .ToDictionaryAsync(summary => summary.RoomId, cancellationToken);

        var rooms = venue.Rooms
            .OrderBy(room => room.Name, StringComparer.OrdinalIgnoreCase)
            .Select(room => summaries.TryGetValue(room.Id, out var summary)
                ? new HostRoomResponse(room.Id, room.Name, room.Capacity, summary.Rules, summary.Exceptions, summary.Revision)
                : new HostRoomResponse(room.Id, room.Name, room.Capacity, 0, 0, 0))
            .ToList();

        return Results.Ok(rooms);
    }

    private static async Task<IResult> GetScheduleAsync(
        Guid venueId,
        Guid roomId,
        HttpContext http,
        VenueFlowDbContext db,
        CancellationToken cancellationToken)
    {
        var (venue, room, problem) = await FindOwnedRoomAsync(db, http, venueId, roomId, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var schedule = await LoadScheduleAsync(db, roomId, cancellationToken) ?? RoomSchedule.Empty(roomId);
        Revisions.SetETag(http.Response, schedule.Revision);
        return Results.Ok(ScheduleWire.ToResponse(venue!, room!, schedule));
    }

    private static async Task<IResult> PutScheduleAsync(
        Guid venueId,
        Guid roomId,
        UpdateRoomScheduleRequest request,
        HttpContext http,
        VenueFlowDbContext db,
        CancellationToken cancellationToken)
    {
        var (venue, room, problem) = await FindOwnedRoomAsync(db, http, venueId, roomId, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var existing = await LoadScheduleAsync(db, roomId, cancellationToken);
        var schedule = existing ?? RoomSchedule.Empty(roomId);

        // A room that never had a schedule is at revision 0, so the editor's
        // first save sends If-Match: "0".
        if (Revisions.CheckIfMatch(http.Request, schedule.Revision) is { } precondition)
        {
            return precondition;
        }

        try
        {
            var rules = (request.Rules ?? Array.Empty<OpeningRuleRequest>()).Select(rule => ScheduleWire.ToInput(rule)).ToList();
            var exceptions = (request.Exceptions ?? Array.Empty<ScheduleExceptionRequest>())
                .Select(exception => ScheduleWire.ToInput(exception))
                .ToList();
            schedule.Replace(rules, exceptions, Guid.CreateVersion7);
        }
        catch (DomainException error)
        {
            return ApiProblems.FromDomain(error);
        }

        if (existing is null)
        {
            db.RoomSchedules.Add(schedule);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error is DbUpdateConcurrencyException || existing is null)
        {
            // Another editor saved first: a changed revision, or (for a first
            // save) a schedule row that now already exists.
            return ApiProblems.Problem(
                StatusCodes.Status412PreconditionFailed,
                "revision_mismatch",
                "The schedule changed while you were saving. Reload and apply your edits again.");
        }

        Revisions.SetETag(http.Response, schedule.Revision);
        return Results.Ok(ScheduleWire.ToResponse(venue!, room!, schedule));
    }

    private static async Task<IResult> ListSlotsAsync(
        Guid venueId,
        Guid roomId,
        string? from,
        string? to,
        HttpContext http,
        VenueFlowDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (venue, room, problem) = await FindOwnedRoomAsync(db, http, venueId, roomId, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(venue!.TimeZoneId);
        ScheduleExpansion expansion;
        DateOnly first;
        DateOnly last;
        try
        {
            // Without dates, show the coming week as the venue's calendar sees it.
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
            first = ScheduleWire.ParseOptionalDate(from, "from") ?? today;
            last = ScheduleWire.ParseOptionalDate(to, "to") ?? first.AddDays(DefaultDays - 1);
            ScheduleExpander.RequireRange(first, last);

            var schedule = await LoadScheduleAsync(db, roomId, cancellationToken, tracking: false);
            expansion = ScheduleExpander.Expand(
                zone,
                schedule?.Rules ?? Array.Empty<OpeningRule>(),
                schedule?.Exceptions ?? Array.Empty<ScheduleException>(),
                room!.Capacity,
                first,
                last);
        }
        catch (DomainException error)
        {
            return ApiProblems.FromDomain(error);
        }

        return Results.Ok(ScheduleWire.ToResponse(roomId, venue.TimeZoneId, first, last, expansion));
    }

    private static async Task<RoomSchedule?> LoadScheduleAsync(
        VenueFlowDbContext db,
        Guid roomId,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        var query = db.RoomSchedules
            .Include(schedule => schedule.Rules)
            .Include(schedule => schedule.Exceptions)
            .Where(schedule => schedule.RoomId == roomId);

        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private static async Task<(Venue? Venue, Room? Room, IResult? Problem)> FindOwnedRoomAsync(
        VenueFlowDbContext db,
        HttpContext http,
        Guid venueId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        var venue = await FindOwnedVenueAsync(db, http, venueId, cancellationToken);
        if (venue is null)
        {
            return (null, null, ApiProblems.NotFound("venue"));
        }

        var room = venue.Rooms.SingleOrDefault(candidate => candidate.Id == roomId);
        return room is null
            ? (venue, null, ApiProblems.NotFound("room"))
            : (venue, room, null);
    }

    private static async Task<Venue?> FindOwnedVenueAsync(
        VenueFlowDbContext db,
        HttpContext http,
        Guid venueId,
        CancellationToken cancellationToken)
    {
        if (http.User.UserId() is not { } hostId)
        {
            return null;
        }

        return await db.Venues
            .AsNoTracking()
            .Include(venue => venue.Rooms)
            .SingleOrDefaultAsync(venue => venue.Id == venueId && venue.HostId == hostId, cancellationToken);
    }
}
