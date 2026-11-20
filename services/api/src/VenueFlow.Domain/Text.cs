namespace VenueFlow.Domain;

internal static class Text
{
    /// <summary>Trims <paramref name="value"/> and checks it is present and not too long.</summary>
    public static string Required(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException($"{field}_required", $"{field} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field}_too_long", $"{field} must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>Trims <paramref name="value"/>; blank becomes null. Checks the length when present.</summary>
    public static string? Optional(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{field}_too_long", $"{field} must be at most {maxLength} characters.");
        }

        return trimmed;
    }
}
