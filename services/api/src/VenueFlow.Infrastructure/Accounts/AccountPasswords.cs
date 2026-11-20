using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using VenueFlow.Domain;
using VenueFlow.Domain.Accounts;

namespace VenueFlow.Infrastructure.Accounts;

/// <summary>
/// Sets and checks account passwords with ASP.NET Core Identity's PBKDF2
/// hasher. Only the hash is stored; plain passwords never leave this class.
/// </summary>
public sealed class AccountPasswords
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    private readonly IPasswordHasher<UserAccount> _hasher;

    // Checked when the email is unknown, so a failed sign-in takes about the
    // same time whether or not the account exists.
    private readonly Lazy<(UserAccount Account, string Hash)> _decoy;

    public AccountPasswords(IPasswordHasher<UserAccount> hasher)
    {
        ArgumentNullException.ThrowIfNull(hasher);
        _hasher = hasher;
        _decoy = new(() =>
        {
            var account = UserAccount.Create(
                Guid.Parse("00000000-0000-0000-0000-00000000dec0"), "decoy@example.invalid", "Decoy", UserRole.Guest);
            return (account, hasher.HashPassword(account, Guid.NewGuid().ToString("N")));
        });
    }

    public void Set(UserAccount account, string password)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (string.IsNullOrEmpty(password) || password.Length < MinLength || password.Length > MaxLength)
        {
            throw new DomainException("password_length_invalid", $"Passwords must be {MinLength}–{MaxLength} characters long.");
        }

        account.SetPasswordHash(_hasher.HashPassword(account, password));
    }

    /// <summary>
    /// Returns true when <paramref name="password"/> matches. If the stored hash
    /// uses older parameters it is upgraded in place; the caller saves it.
    /// A null <paramref name="account"/> always fails, after the same amount of work.
    /// </summary>
    public bool Verify([NotNullWhen(true)] UserAccount? account, string password)
    {
        if (account is null || account.PasswordHash.Length == 0)
        {
            var decoy = _decoy.Value;
            _ = _hasher.VerifyHashedPassword(decoy.Account, decoy.Hash, password);
            return false;
        }

        var result = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.SetPasswordHash(_hasher.HashPassword(account, password));
        }

        return result != PasswordVerificationResult.Failed;
    }
}
