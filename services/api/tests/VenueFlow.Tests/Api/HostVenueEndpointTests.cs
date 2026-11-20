using System.Net;
using System.Net.Http.Json;
using VenueFlow.Api.Venues;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Api;

public sealed class HostVenueEndpointTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private static object CompleteBody(string name) => new
    {
        name,
        timeZoneId = "Europe/Madrid",
        description = "Bright studio with long tables.",
        city = "Madrid",
        addressLine = "Calle Falsa 1 (synthetic)",
        accessibilityNotes = "Step-free entrance.",
        rooms = new[] { new { id = (Guid?)null, name = "Main room", capacity = 16 } },
    };

    [Fact]
    public async Task Anonymous_callers_get_401_and_non_hosts_get_403()
    {
        using var anonymous = factory.CreateClient();
        using var guest = await factory.SignedInClientAsync("guest@example.test");
        using var admin = await factory.SignedInClientAsync("admin@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/host/venues")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await guest.GetAsync("/api/host/venues")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/host/venues", new { name = "X", timeZoneId = "UTC" })).StatusCode);
    }

    [Fact]
    public async Task Hosts_only_list_their_own_venues()
    {
        using var host = await factory.SignedInClientAsync("host@example.test");
        using var other = await factory.SignedInClientAsync("other-host@example.test");
        var (otherDraft, _) = await other.CreateDraftAsync("Other host's studio");

        var mine = await host.GetFromJsonAsync<List<VenueSummaryResponse>>("/api/host/venues");
        var theirs = await other.GetFromJsonAsync<List<VenueSummaryResponse>>("/api/host/venues");

        Assert.Contains(mine!, venue => venue.Id == DemoVenues.RiversideStudioId && venue.Status == "Published");
        Assert.DoesNotContain(mine!, venue => venue.Id == otherDraft.Id);
        Assert.Equal(otherDraft.Id, Assert.Single(theirs!).Id);
    }

    [Fact]
    public async Task Another_hosts_venue_answers_404_for_reads_and_writes()
    {
        using var other = await factory.SignedInClientAsync("other-host@example.test");
        var id = DemoVenues.RiversideStudioId;

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/host/venues/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/host/venues/{id}/preview")).StatusCode);
        using var put = await other.PutDraftAsync(id, CompleteBody("Hijacked"), "*");
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task Create_returns_a_draft_with_location_and_etag()
    {
        using var host = await factory.SignedInClientAsync("host@example.test");

        using var response = await host.PostAsJsonAsync("/api/host/venues", new { name = "  Pottery Corner ", timeZoneId = "America/Bogota" });
        var draft = await response.Content.ReadFromJsonAsync<VenueDraftResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/host/venues/{draft!.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal("\"1\"", response.Headers.ETag!.ToString());
        Assert.Equal(("Pottery Corner", "Draft", 1), (draft.Name, draft.Status, draft.Revision));
        Assert.Equal(new[] { "description", "city", "address_line", "rooms" }, draft.Missing);
    }

    [Fact]
    public async Task Create_rejects_invalid_fields_with_a_code()
    {
        using var host = await factory.SignedInClientAsync("host@example.test");

        using var response = await host.PostAsJsonAsync("/api/host/venues", new { name = "Studio", timeZoneId = "Romance Standard Time" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("time_zone_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Update_requires_the_current_etag()
    {
        using var host = await factory.SignedInClientAsync("host@example.test");
        var (draft, etag) = await host.CreateDraftAsync();

        using var missing = await host.PutDraftAsync(draft.Id, CompleteBody("Studio"), ifMatch: null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        using var saved = await host.PutDraftAsync(draft.Id, CompleteBody("Studio v2"), etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("\"2\"", saved.Headers.ETag!.ToString());

        // A second editor still holding revision 1 must not overwrite revision 2.
        using var stale = await host.PutDraftAsync(draft.Id, CompleteBody("Stale edit"), etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("revision_mismatch", await stale.ProblemCodeAsync());

        var current = await host.GetFromJsonAsync<VenueDraftResponse>($"/api/host/venues/{draft.Id}");
        Assert.Equal("Studio v2", current!.Name);
        Assert.Equal("Main room", Assert.Single(current.Rooms).Name);
        Assert.Empty(current.Missing);
    }

    [Fact]
    public async Task Preview_shows_the_guest_view_without_the_street_address()
    {
        using var host = await factory.SignedInClientAsync("host@example.test");
        var (draft, etag) = await host.CreateDraftAsync();
        (await host.PutDraftAsync(draft.Id, CompleteBody("Preview studio"), etag)).Dispose();

        using var response = await host.GetAsync($"/api/host/venues/{draft.Id}/preview");
        var json = await response.Content.ReadAsStringAsync();
        var preview = await response.Content.ReadFromJsonAsync<VenuePreviewResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(preview!.ReadyToSubmit);
        Assert.Equal(16, preview.LargestRoomCapacity);
        Assert.Equal("Step-free entrance.", preview.AccessibilityNotes);
        Assert.DoesNotContain("Calle Falsa", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_needs_a_complete_draft_and_locks_editing()
    {
        using var host = await factory.SignedInClientAsync("host@example.test");
        var (draft, etag) = await host.CreateDraftAsync();

        using var incomplete = await host.SubmitAsync(draft.Id, etag);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, incomplete.StatusCode);
        Assert.Equal("venue_incomplete", await incomplete.ProblemCodeAsync());

        using var saved = await host.PutDraftAsync(draft.Id, CompleteBody("Ready studio"), etag);
        using var submitted = await host.SubmitAsync(draft.Id, saved.Headers.ETag!.ToString());
        var inReview = await submitted.Content.ReadFromJsonAsync<VenueDraftResponse>();
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal("InReview", inReview!.Status);

        using var edit = await host.PutDraftAsync(draft.Id, CompleteBody("Too late"), submitted.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("venue_not_editable", await edit.ProblemCodeAsync());
    }
}
