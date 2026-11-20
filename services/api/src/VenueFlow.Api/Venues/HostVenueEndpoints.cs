using Microsoft.EntityFrameworkCore;
using VenueFlow.Api.Auth;
using VenueFlow.Api.Http;
using VenueFlow.Domain;
using VenueFlow.Domain.Venues;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Api.Venues;

/// <summary>
/// The host's own listings. Every route requires a host account, and every
/// lookup is scoped to the caller's id: another host's venue answers 404, the
/// same as one that does not exist, so ids cannot be probed.
/// </summary>
internal static class HostVenueEndpoints
{
    public static void MapHostVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var venues = app.MapGroup("/api/host/venues")
            .WithTags("Host venues")
            .RequireAuthorization(AuthPolicies.Host);

        venues.MapGet("/", ListAsync)
            .WithName("ListHostVenues")
            .WithSummary("Venues owned by the signed-in host, any status")
            .Produces<IReadOnlyList<VenueSummaryResponse>>();

        venues.MapPost("/", CreateAsync)
            .WithName("CreateVenueDraft")
            .WithSummary("Start a new venue draft")
            .Produces<VenueDraftResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        venues.MapGet("/{id:guid}", GetAsync)
            .WithName("GetVenueDraft")
            .WithSummary("One of the host's venues, with its ETag")
            .Produces<VenueDraftResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        venues.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateVenueDraft")
            .WithSummary("Replace a draft's details and rooms; requires If-Match")
            .Produces<VenueDraftResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);

        venues.MapGet("/{id:guid}/preview", PreviewAsync)
            .WithName("PreviewVenueListing")
            .WithSummary("How the listing will look to guests, plus what is missing before submission")
            .Produces<VenuePreviewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        venues.MapPost("/{id:guid}/submit", SubmitAsync)
            .WithName("SubmitVenueForReview")
            .WithSummary("Send a complete draft to an admin for review; requires If-Match")
            .Produces<VenueDraftResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired);
    }

    private static async Task<IResult> ListAsync(HttpContext http, VenueFlowDbContext db, CancellationToken cancellationToken)
    {
        var hostId = http.User.UserId();
        var rows = await db.Venues
            .AsNoTracking()
            .Where(venue => venue.HostId == hostId)
            .OrderBy(venue => venue.Name)
            .Select(venue => new { venue.Id, venue.Name, venue.City, venue.Status, RoomCount = venue.Rooms.Count(), venue.Revision })
            .ToListAsync(cancellationToken);

        var venues = rows
            .Select(row => new VenueSummaryResponse(row.Id, row.Name, row.City, row.Status.ToString(), row.RoomCount, row.Revision))
            .ToList();

        return Results.Ok(venues);
    }

    private static async Task<IResult> CreateAsync(
        CreateVenueDraftRequest request,
        HttpContext http,
        VenueFlowDbContext db,
        CancellationToken cancellationToken)
    {
        if (http.User.UserId() is not { } hostId)
        {
            return Results.Unauthorized();
        }

        Venue venue;
        try
        {
            venue = Venue.Create(Guid.CreateVersion7(), hostId, request.Name ?? string.Empty, request.TimeZoneId ?? string.Empty);
        }
        catch (DomainException error)
        {
            return ApiProblems.FromDomain(error);
        }

        db.Venues.Add(venue);
        await db.SaveChangesAsync(cancellationToken);

        Revisions.SetETag(http.Response, venue.Revision);
        return Results.Created($"/api/host/venues/{venue.Id}", VenueDraftResponse.From(venue));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext http, VenueFlowDbContext db, CancellationToken cancellationToken)
    {
        var venue = await FindOwnedAsync(db, http, id, cancellationToken);
        if (venue is null)
        {
            return ApiProblems.NotFound("venue");
        }

        Revisions.SetETag(http.Response, venue.Revision);
        return Results.Ok(VenueDraftResponse.From(venue));
    }

    private static async Task<IResult> PreviewAsync(Guid id, HttpContext http, VenueFlowDbContext db, CancellationToken cancellationToken)
    {
        var venue = await FindOwnedAsync(db, http, id, cancellationToken);
        if (venue is null)
        {
            return ApiProblems.NotFound("venue");
        }

        Revisions.SetETag(http.Response, venue.Revision);
        return Results.Ok(VenuePreviewResponse.From(venue));
    }

    private static Task<IResult> UpdateAsync(
        Guid id,
        UpdateVenueDraftRequest request,
        HttpContext http,
        VenueFlowDbContext db,
        CancellationToken cancellationToken) =>
        MutateAsync(id, http, db, cancellationToken, venue =>
            venue.EditDraft(
                request.Name ?? string.Empty,
                request.TimeZoneId ?? string.Empty,
                request.Description,
                request.City,
                request.AddressLine,
                request.AccessibilityNotes,
                request.Rooms?.Select(room => new RoomInput(room.Id, room.Name ?? string.Empty, room.Capacity)).ToList(),
                Guid.CreateVersion7));

    private static Task<IResult> SubmitAsync(Guid id, HttpContext http, VenueFlowDbContext db, CancellationToken cancellationToken) =>
        MutateAsync(id, http, db, cancellationToken, venue => venue.SubmitForReview());

    /// <summary>
    /// Shared write path: ownership, If-Match, the domain change, then a save
    /// that the revision concurrency token guards against a racing editor.
    /// </summary>
    private static async Task<IResult> MutateAsync(
        Guid id,
        HttpContext http,
        VenueFlowDbContext db,
        CancellationToken cancellationToken,
        Action<Venue> change)
    {
        var venue = await FindOwnedAsync(db, http, id, cancellationToken);
        if (venue is null)
        {
            return ApiProblems.NotFound("venue");
        }

        if (Revisions.CheckIfMatch(http.Request, venue.Revision) is { } precondition)
        {
            return precondition;
        }

        try
        {
            change(venue);
        }
        catch (DomainException error)
        {
            return ApiProblems.FromDomain(error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Problem(
                StatusCodes.Status412PreconditionFailed,
                "revision_mismatch",
                "The listing changed while you were saving. Reload and apply your edits again.");
        }

        Revisions.SetETag(http.Response, venue.Revision);
        return Results.Ok(VenueDraftResponse.From(venue));
    }

    private static async Task<Venue?> FindOwnedAsync(
        VenueFlowDbContext db,
        HttpContext http,
        Guid id,
        CancellationToken cancellationToken)
    {
        var hostId = http.User.UserId();
        if (hostId is null)
        {
            return null;
        }

        return await db.Venues
            .Include(venue => venue.Rooms)
            .SingleOrDefaultAsync(venue => venue.Id == id && venue.HostId == hostId.Value, cancellationToken);
    }
}
