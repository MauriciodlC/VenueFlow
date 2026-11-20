using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using VenueFlow.Api.Venues;

namespace VenueFlow.Tests.Api;

internal static class ApiClientExtensions
{
    /// <summary>A PUT body that leaves nothing missing, with one new room.</summary>
    public static object CompleteDraftBody(string name) => new
    {
        name,
        timeZoneId = "Europe/Madrid",
        description = "Bright studio with long tables.",
        city = "Madrid",
        addressLine = "Calle Falsa 1 (synthetic)",
        accessibilityNotes = "Step-free entrance.",
        rooms = new[] { new { id = (Guid?)null, name = "Main room", capacity = 16 } },
    };

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = SeededApiFactory.DemoPassword) =>
        await client.PostAsJsonAsync("/api/auth/login", new { email, password });

    /// <summary>A client with its own cookie jar, signed in as <paramref name="email"/>.</summary>
    public static async Task<HttpClient> SignedInClientAsync(this SeededApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        using var response = await client.LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    public static async Task<(VenueDraftResponse Draft, string ETag)> CreateDraftAsync(this HttpClient client, string name = "Test studio")
    {
        using var response = await client.PostAsJsonAsync("/api/host/venues", new { name, timeZoneId = "Europe/Madrid" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<VenueDraftResponse>();
        return (draft!, response.Headers.ETag!.ToString());
    }

    public static async Task<HttpResponseMessage> PutDraftAsync(this HttpClient client, Guid id, object body, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/host/venues/{id}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> SubmitAsync(this HttpClient client, Guid id, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/host/venues/{id}/submit");
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(ifMatch));
        }

        return await client.SendAsync(request);
    }

    /// <summary>Creates a draft and fills it in, ready to submit. Returns the current ETag.</summary>
    public static async Task<(Guid Id, string ETag)> CreateCompleteDraftAsync(this HttpClient client, string name)
    {
        var (draft, etag) = await client.CreateDraftAsync(name);
        using var saved = await client.PutDraftAsync(draft.Id, CompleteDraftBody(name), etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        return (draft.Id, saved.Headers.ETag!.ToString());
    }

    /// <summary>The owner's current view of a venue and its ETag.</summary>
    public static async Task<(VenueDraftResponse Venue, string ETag)> ReadVenueAsync(this HttpClient client, Guid id)
    {
        using var response = await client.GetAsync($"/api/host/venues/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var venue = await response.Content.ReadFromJsonAsync<VenueDraftResponse>();
        return (venue!, response.Headers.ETag!.ToString());
    }

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
