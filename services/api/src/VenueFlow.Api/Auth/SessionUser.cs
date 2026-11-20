using System.Security.Claims;
using VenueFlow.Domain.Accounts;

namespace VenueFlow.Api.Auth;

/// <summary>The signed-in user as the web client sees it. Roles are lower case.</summary>
public sealed record SessionUser(Guid Id, string Email, string DisplayName, string Role)
{
    public static SessionUser From(UserAccount account) =>
        new(account.Id, account.Email, account.DisplayName, account.Role.ToString().ToLowerInvariant());

    public static SessionUser? From(ClaimsPrincipal principal)
    {
        var id = principal.UserId();
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var name = principal.FindFirstValue(ClaimTypes.Name);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        return id is null || email is null || name is null || role is null
            ? null
            : new SessionUser(id.Value, email, name, role.ToLowerInvariant());
    }

    public ClaimsPrincipal ToPrincipal(string authenticationType, UserRole role) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Id.ToString()),
                new Claim(ClaimTypes.Email, Email),
                new Claim(ClaimTypes.Name, DisplayName),
                new Claim(ClaimTypes.Role, role.ToString()),
            },
            authenticationType));
}

internal static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
