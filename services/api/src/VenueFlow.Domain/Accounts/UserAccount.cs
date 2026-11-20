namespace VenueFlow.Domain.Accounts;

/// <summary>
/// A person who can sign in. Emails are stored normalized (trimmed, lower case)
/// so the unique index also catches addresses that differ only in case.
/// </summary>
public sealed class UserAccount
{
    public const int EmailMaxLength = 254;
    public const int DisplayNameMaxLength = 100;
    public const int PasswordHashMaxLength = 512;

    private UserAccount()
    {
        // Used by EF Core.
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    /// <summary>Opaque hash produced by the infrastructure layer. Empty until a password is set.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public bool IsHost => Role == UserRole.Host;

    public static UserAccount Create(Guid id, string email, string displayName, UserRole role)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("user_id_required", "A user needs a non-empty id.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new DomainException("user_role_invalid", $"'{role}' is not a known role.");
        }

        return new UserAccount
        {
            Id = id,
            Email = NormalizeEmail(email),
            DisplayName = Text.Required(displayName, DisplayNameMaxLength, "display_name"),
            Role = role,
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > PasswordHashMaxLength)
        {
            throw new DomainException("password_hash_invalid", "The password hash is empty or too long.");
        }

        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Trims and lower-cases an address and checks its basic shape (one '@'
    /// with text on both sides). Deliverability is not checked.
    /// </summary>
    public static string NormalizeEmail(string? email)
    {
        var normalized = Text.Required(email, EmailMaxLength, "email").ToLowerInvariant();
        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0
            || at != normalized.LastIndexOf('@')
            || at == normalized.Length - 1
            || normalized.Any(char.IsWhiteSpace))
        {
            throw new DomainException("email_invalid", "Email address is not valid.");
        }

        return normalized;
    }
}
