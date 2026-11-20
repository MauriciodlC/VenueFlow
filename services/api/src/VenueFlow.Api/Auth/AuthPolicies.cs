using VenueFlow.Domain.Accounts;

namespace VenueFlow.Api.Auth;

internal static class AuthPolicies
{
    /// <summary>Signed-in host accounts. Ownership of a specific venue is checked per request.</summary>
    public const string Host = "host";

    public static void Add(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(Host, policy => policy.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Host)));
    }
}
