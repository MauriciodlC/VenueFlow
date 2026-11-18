using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VenueFlow.Tests.Api;

public sealed class HostingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_is_healthy_without_running_dependency_checks()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Empty(body.GetProperty("checks").EnumerateObject());
    }

    [Fact]
    public async Task Info_returns_service_name_and_version()
    {
        using var client = factory.CreateClient();

        var info = await client.GetFromJsonAsync<ServiceInfo>("/api/info");

        Assert.NotNull(info);
        Assert.Equal("venueflow-api", info.Name);
        Assert.Matches(@"^\d+\.\d+\.\d+$", info.Version);
    }

    [Fact]
    public async Task OpenApi_document_lists_the_info_operation()
    {
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        Assert.True(document.GetProperty("paths").TryGetProperty("/api/info", out var path));
        Assert.Equal("GetServiceInfo", path.GetProperty("get").GetProperty("operationId").GetString());
    }
}
