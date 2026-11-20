using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Api;

/// <summary>
/// Denial paths for the host venue API: callers who are not the owner, and
/// lifecycle moves the venue's current status does not allow. Every denied
/// request must also leave the venue unchanged.
/// </summary>
public sealed class VenueAccessAndTransitionTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private const string Host = "host@example.test";
    private const string OtherHost = "other-host@example.test";

    private static readonly Guid RiversideWorkshopHallId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e11");

    public static TheoryData<string> NonHostAccounts => new() { "guest@example.test", "staff@example.test", "admin@example.test" };

    private static IEnumerable<Func<HttpClient, Task<HttpResponseMessage>>> EveryVenueRoute(Guid id) =>
    [
        client => client.GetAsync($"/api/host/venues/{id}"),
        client => client.GetAsync($"/api/host/venues/{id}/preview"),
        client => client.PutDraftAsync(id, ApiClientExtensions.CompleteDraftBody("Hijacked"), "*"),
        client => client.SubmitAsync(id, "*"),
    ];

    [Fact]
    public async Task Another_host_cannot_read_edit_or_submit_a_draft_and_it_stays_unchanged()
    {
        using var owner = await factory.SignedInClientAsync(Host);
        using var intruder = await factory.SignedInClientAsync(OtherHost);
        var (id, etag) = await owner.CreateCompleteDraftAsync("Owner's ready draft");

        foreach (var call in EveryVenueRoute(id))
        {
            using var response = await call(intruder);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("venue_not_found", await response.ProblemCodeAsync());
        }

        var (venue, current) = await owner.ReadVenueAsync(id);
        Assert.Equal(("Owner's ready draft", "Draft"), (venue.Name, venue.Status));
        Assert.Equal(etag, current);
    }

    [Fact]
    public async Task A_foreign_venue_looks_exactly_like_an_unknown_id()
    {
        using var intruder = await factory.SignedInClientAsync(OtherHost);

        using var foreign = await intruder.GetAsync($"/api/host/venues/{DemoVenues.RiversideStudioId}");
        using var unknown = await intruder.GetAsync($"/api/host/venues/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(unknown.StatusCode, foreign.StatusCode);
        Assert.Equal(await ProblemAsync(unknown), await ProblemAsync(foreign));
        Assert.False(foreign.Headers.Contains("ETag"));
    }

    [Fact]
    public async Task A_host_cannot_take_over_another_hosts_room_by_id()
    {
        using var owner = await factory.SignedInClientAsync(Host);
        using var intruder = await factory.SignedInClientAsync(OtherHost);
        var (draft, etag) = await intruder.CreateDraftAsync("Room grabber");

        var body = new
        {
            name = "Room grabber",
            timeZoneId = "Europe/Madrid",
            rooms = new[] { new { id = (Guid?)RiversideWorkshopHallId, name = "Stolen hall", capacity = 999 } },
        };
        using var put = await intruder.PutDraftAsync(draft.Id, body, etag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, put.StatusCode);
        Assert.Equal("room_not_found", await put.ProblemCodeAsync());

        var (riverside, _) = await owner.ReadVenueAsync(DemoVenues.RiversideStudioId);
        Assert.Contains(riverside.Rooms, room => room.Id == RiversideWorkshopHallId && room.Name == "Workshop Hall" && room.Capacity == 24);
        var (grabber, _) = await intruder.ReadVenueAsync(draft.Id);
        Assert.Empty(grabber.Rooms);
    }

    [Theory]
    [MemberData(nameof(NonHostAccounts))]
    public async Task Non_host_roles_are_forbidden_on_every_venue_route(string email)
    {
        using var client = await factory.SignedInClientAsync(email);

        foreach (var call in EveryVenueRoute(DemoVenues.RiversideStudioId))
        {
            using var response = await call(client);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Signed_out_callers_are_unauthorized_on_every_venue_route()
    {
        using var client = await factory.SignedInClientAsync(Host);
        (await client.PostAsync("/api/auth/logout", content: null)).Dispose();

        foreach (var call in EveryVenueRoute(DemoVenues.RiversideStudioId))
        {
            using var response = await call(client);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Submitting_a_venue_already_in_review_is_a_conflict()
    {
        using var host = await factory.SignedInClientAsync(Host);
        var (id, etag) = await host.CreateCompleteDraftAsync("Submitted twice");
        using var first = await host.SubmitAsync(id, etag);
        var inReviewETag = first.Headers.ETag!.ToString();

        using var second = await host.SubmitAsync(id, inReviewETag);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("venue_status_transition_invalid", await second.ProblemCodeAsync());
        var (venue, current) = await host.ReadVenueAsync(id);
        Assert.Equal("InReview", venue.Status);
        Assert.Equal(inReviewETag, current);
    }

    [Fact]
    public async Task A_published_venue_can_be_neither_resubmitted_nor_edited()
    {
        using var host = await factory.SignedInClientAsync(Host);
        var (before, etag) = await host.ReadVenueAsync(DemoVenues.HarborLoftId);
        Assert.Equal("Published", before.Status);

        using var submit = await host.SubmitAsync(DemoVenues.HarborLoftId, etag);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Equal("venue_status_transition_invalid", await submit.ProblemCodeAsync());

        using var edit = await host.PutDraftAsync(DemoVenues.HarborLoftId, ApiClientExtensions.CompleteDraftBody("Renamed loft"), etag);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("venue_not_editable", await edit.ProblemCodeAsync());

        var (after, current) = await host.ReadVenueAsync(DemoVenues.HarborLoftId);
        Assert.Equal((before.Name, "Published", before.Revision), (after.Name, after.Status, after.Revision));
        Assert.Equal(etag, current);
    }

    [Fact]
    public async Task Submit_without_if_match_or_with_a_stale_etag_leaves_the_draft_as_is()
    {
        using var host = await factory.SignedInClientAsync(Host);
        var (draft, staleETag) = await host.CreateDraftAsync("Careful submitter");
        using var saved = await host.PutDraftAsync(draft.Id, ApiClientExtensions.CompleteDraftBody("Careful submitter"), staleETag);
        var currentETag = saved.Headers.ETag!.ToString();

        using var missing = await host.SubmitAsync(draft.Id, ifMatch: null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal("if_match_required", await missing.ProblemCodeAsync());

        using var stale = await host.SubmitAsync(draft.Id, staleETag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("revision_mismatch", await stale.ProblemCodeAsync());

        var (venue, etag) = await host.ReadVenueAsync(draft.Id);
        Assert.Equal("Draft", venue.Status);
        Assert.Equal(currentETag, etag);
    }

    [Fact]
    public async Task An_incomplete_draft_is_rejected_with_the_missing_fields()
    {
        using var host = await factory.SignedInClientAsync(Host);
        var (draft, etag) = await host.CreateDraftAsync("Half done");

        using var response = await host.SubmitAsync(draft.Id, etag);
        var detail = (await ProblemAsync(response)).Detail;

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(detail);
        Assert.Contains("description, city, address_line, rooms", detail, StringComparison.Ordinal);
        var (venue, current) = await host.ReadVenueAsync(draft.Id);
        Assert.Equal("Draft", venue.Status);
        Assert.Equal(etag, current);
    }

    private static async Task<(string? Code, string? Detail)> ProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            body.TryGetProperty("code", out var code) ? code.GetString() : null,
            body.TryGetProperty("detail", out var detail) ? detail.GetString() : null);
    }
}
