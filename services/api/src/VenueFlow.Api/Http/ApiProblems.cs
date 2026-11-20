using VenueFlow.Domain;

namespace VenueFlow.Api.Http;

/// <summary>
/// Problem Details responses with a stable <c>code</c> extension the web client
/// can switch on (it never has to parse the human-readable detail).
/// </summary>
internal static class ApiProblems
{
    // Rule violations caused by the listing's current state rather than by the input.
    private static readonly HashSet<string> ConflictCodes = new(StringComparer.Ordinal)
    {
        "venue_not_editable",
        "venue_status_transition_invalid",
    };

    public static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            statusCode: status,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    public static IResult FromDomain(DomainException error) =>
        Problem(
            ConflictCodes.Contains(error.Code) ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity,
            error.Code,
            error.Message);

    public static IResult NotFound(string what) =>
        Problem(StatusCodes.Status404NotFound, $"{what}_not_found", $"No {what.Replace('_', ' ')} with that id.");
}
