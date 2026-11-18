namespace VenueFlow.Domain;

/// <summary>
/// Raised when an operation would break a domain rule. <see cref="Code"/> is a
/// stable, machine-readable value that the API maps to an error response.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
