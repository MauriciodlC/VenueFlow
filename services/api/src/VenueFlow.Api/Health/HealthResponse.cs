using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace VenueFlow.Api.Health;

/// <summary>
/// Writes the overall status and one status per check. Exception details are
/// left out so connection strings and hostnames never reach the response.
/// </summary>
internal static class HealthResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString()),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
