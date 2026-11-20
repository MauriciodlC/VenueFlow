using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VenueFlow.Api.Http;
using VenueFlow.Domain;
using VenueFlow.Domain.Accounts;
using VenueFlow.Infrastructure.Accounts;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Api.Auth;

public sealed record LoginRequest(string? Email, string? Password);

/// <summary>
/// Cookie sign-in for the Vue app. The session cookie is HttpOnly and
/// SameSite=Strict, so other sites can neither read it nor send it along.
/// </summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Sign in with email and password; sets the session cookie")
            .Produces<SessionUser>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("End the session")
            .Produces(StatusCodes.Status204NoContent);

        auth.MapGet("/me", (HttpContext http) =>
                SessionUser.From(http.User) is { } user ? Results.Ok(user) : Results.Unauthorized())
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user")
            .RequireAuthorization()
            .Produces<SessionUser>();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        VenueFlowDbContext db,
        AccountPasswords passwords,
        CancellationToken cancellationToken)
    {
        UserAccount? account = null;
        try
        {
            var email = UserAccount.NormalizeEmail(request.Email);
            account = await db.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        }
        catch (DomainException)
        {
            // A malformed address is reported exactly like a wrong password.
        }

        if (!passwords.Verify(account, request.Password ?? string.Empty))
        {
            return ApiProblems.Problem(
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                "Email or password is incorrect.");
        }

        // Verify may have upgraded an old hash.
        await db.SaveChangesAsync(cancellationToken);

        var user = SessionUser.From(account);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            user.ToPrincipal(CookieAuthenticationDefaults.AuthenticationScheme, account.Role));

        return Results.Ok(user);
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }
}
