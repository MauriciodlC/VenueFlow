using VenueFlow.Domain.Accounts;

namespace VenueFlow.Infrastructure.Seeding;

/// <summary>
/// One synthetic account per role. Ids are fixed so venues, tests and demo
/// scripts can refer to them; <c>@example.test</c> addresses cannot receive mail.
/// Passwords are not part of the seed: they come from <c>Demo:Password</c>.
/// </summary>
public static class DemoAccounts
{
    public static readonly Guid GuestId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9a01");

    public static readonly Guid HostId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9a02");

    public static readonly Guid StaffId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9a03");

    public static readonly Guid AdminId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9a04");

    /// <summary>A second host that owns nothing; used to show cross-host access is denied.</summary>
    public static readonly Guid OtherHostId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9a05");

    /// <summary>Builds new accounts without password hashes on every call.</summary>
    public static IReadOnlyList<UserAccount> Build() =>
    [
        UserAccount.Create(GuestId, "guest@example.test", "Gina Guest", UserRole.Guest),
        UserAccount.Create(HostId, "host@example.test", "Hugo Host", UserRole.Host),
        UserAccount.Create(StaffId, "staff@example.test", "Sam Staff", UserRole.Staff),
        UserAccount.Create(AdminId, "admin@example.test", "Ada Admin", UserRole.Admin),
        UserAccount.Create(OtherHostId, "other-host@example.test", "Olga Other-Host", UserRole.Host),
    ];
}
