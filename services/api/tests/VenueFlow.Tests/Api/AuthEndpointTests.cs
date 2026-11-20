using System.Net;
using System.Net.Http.Json;
using VenueFlow.Api.Auth;
using VenueFlow.Infrastructure.Seeding;

namespace VenueFlow.Tests.Api;

public sealed class AuthEndpointTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    [Fact]
    public async Task Login_sets_a_strict_http_only_session_cookie_and_returns_the_user()
    {
        using var client = factory.CreateClient();

        using var response = await client.LoginAsync(" Host@Example.test ");
        var user = await response.Content.ReadFromJsonAsync<SessionUser>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SessionUser(DemoAccounts.HostId, "host@example.test", "Hugo Host", "host"), user);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("venueflow.session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("host@example.test", "wrong-password-123")]
    [InlineData("nobody@example.test", SeededApiFactory.DemoPassword)]
    [InlineData("not-an-email", SeededApiFactory.DemoPassword)]
    public async Task Login_rejects_bad_credentials_with_the_same_answer(string email, string password)
    {
        using var client = factory.CreateClient();

        using var response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", await response.ProblemCodeAsync());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Me_requires_a_session_and_logout_ends_it()
    {
        using var client = factory.CreateClient();

        using (var anonymous = await client.GetAsync("/api/auth/me"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        (await client.LoginAsync("guest@example.test")).Dispose();
        var me = await client.GetFromJsonAsync<SessionUser>("/api/auth/me");
        Assert.Equal("guest", me!.Role);

        (await client.PostAsync("/api/auth/logout", content: null)).Dispose();
        using var afterLogout = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
