namespace VenueFlow.Domain.Accounts;

/// <summary>
/// The single role a user account holds (see docs/domain/personas.md). Stored
/// by name, so the numeric values are not part of the schema.
/// </summary>
public enum UserRole
{
    Guest,
    Host,
    Staff,
    Admin,
}
