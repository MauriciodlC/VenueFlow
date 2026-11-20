using Microsoft.Net.Http.Headers;

namespace VenueFlow.Api.Http;

/// <summary>
/// Maps an aggregate revision to a strong ETag (<c>"7"</c>) and checks
/// <c>If-Match</c> on writes. Writes without <c>If-Match</c> are refused, so a
/// client can never overwrite changes it has not seen.
/// </summary>
internal static class Revisions
{
    public static string ToETag(int revision) => $"\"{revision}\"";

    public static void SetETag(HttpResponse response, int revision) =>
        response.Headers.ETag = ToETag(revision);

    /// <summary>Returns an error result, or null when the request may proceed.</summary>
    public static IResult? CheckIfMatch(HttpRequest request, int currentRevision)
    {
        var ifMatch = request.GetTypedHeaders().IfMatch;
        if (ifMatch is null || ifMatch.Count == 0)
        {
            return ApiProblems.Problem(
                StatusCodes.Status428PreconditionRequired,
                "if_match_required",
                "Send the ETag you last read in an If-Match header.");
        }

        var current = new EntityTagHeaderValue(ToETag(currentRevision));
        var matches = ifMatch.Any(tag =>
            tag.Equals(EntityTagHeaderValue.Any) || (!tag.IsWeak && tag.Tag == current.Tag));

        return matches
            ? null
            : ApiProblems.Problem(
                StatusCodes.Status412PreconditionFailed,
                "revision_mismatch",
                "The listing changed since you loaded it. Reload and apply your edits again.");
    }
}
